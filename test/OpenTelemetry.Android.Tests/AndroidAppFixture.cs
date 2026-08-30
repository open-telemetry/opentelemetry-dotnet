// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Android.Tests;

/// <summary>
/// Starts the in-process OTLP collector and then drives the Android test app on a
/// connected emulator. The device run is executed once for the whole class: the
/// app emits traces, metrics and logs over OTLP/HTTP to the collector and the
/// tests then assert on what was received.
/// </summary>
/// <remarks>
/// An Android emulator must already be running. The 'android' workload is required
/// to build the app and 'adb' must be on the PATH (both are provided by the CI
/// workflow). The app is built and installed with 'dotnet build -t:Install' and run
/// with 'adb shell am instrument'; 'dotnet test' cannot drive on-device Android in
/// the current workload (its Microsoft.Testing.Platform pipe is not reachable from
/// the device).
/// </remarks>
public sealed class AndroidAppFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private const string InstrumentationComponent =
        "io.opentelemetry.dotnet.android/io.opentelemetry.dotnet.android.TestInstrumentation";

    private static readonly TimeSpan BuildAndInstallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InstrumentationTimeout = TimeSpan.FromMinutes(5);

    internal OtlpHttpCollector Collector { get; private set; } = null!;

    internal int DeviceRunExitCode { get; private set; }

    internal string DeviceRunOutput { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Bind on all interfaces so the emulator can reach the collector via
        // 10.0.2.2 (the alias for the host loopback).
        var baseUrl = $"http://0.0.0.0:4318";
        this.Collector = await OtlpHttpCollector.StartAsync(baseUrl);

        var repoRoot = RepoRoot();
        var project = Path.Combine(repoRoot, "test", "OpenTelemetry.Android.TestApp", "OpenTelemetry.Android.TestApp.csproj");
        var tfm = AndroidTargetFramework();

        // Build the APK and install it on the connected emulator.
        var (installExitCode, installOutput) = RunProcess(
            "dotnet",
            ["build", project, "--configuration", Configuration, "--framework", tfm, "-t:Install"],
            repoRoot,
            BuildAndInstallTimeout);

        if (installExitCode != 0)
        {
            this.DeviceRunExitCode = installExitCode;
            this.DeviceRunOutput = "APK build/install failed." + Environment.NewLine + installOutput;
            return;
        }

        // Run the on-device instrumentation synchronously. It executes the tests via
        // Microsoft.Testing.Platform on the device, which export OTLP to the collector.
        var (runExitCode, runOutput) = RunProcess(
            "adb",
            ["shell", "am", "instrument", "-w", InstrumentationComponent],
            repoRoot,
            InstrumentationTimeout);

        // 'am instrument' exits 0 even when tests fail; success is signalled by the
        // instrumentation result: Result.Ok (INSTRUMENTATION_CODE: -1) with failed=0.
        var succeeded =
            runExitCode == 0 &&
            runOutput.Contains("INSTRUMENTATION_CODE: -1", StringComparison.Ordinal) &&
            runOutput.Contains("failed=0", StringComparison.Ordinal);

        this.DeviceRunExitCode = succeeded ? 0 : 1;
        this.DeviceRunOutput = installOutput + Environment.NewLine + runOutput;
    }

    public async Task DisposeAsync()
    {
        if (this.Collector is not null)
        {
            await this.Collector.DisposeAsync();
        }
    }

    private static string AndroidTargetFramework()
    {
        var frameworkName = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        if (frameworkName is not null)
        {
            const string Marker = "Version=v";
            var index = frameworkName.IndexOf(Marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                return $"net{frameworkName[(index + Marker.Length)..]}-android";
            }
        }

        return $"net{Environment.Version.ToString(2)}-android";
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenTelemetry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (OpenTelemetry.slnx).");
    }

    private static (int ExitCode, string Output) RunProcess(string fileName, string[] arguments, string workingDirectory, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        // Stop the persistent build servers from inheriting the redirected handles,
        // otherwise WaitForExit can block on a build server that idle-times-out long
        // after the command itself finished.
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");

        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeout))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Race shutting down the process
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Kill failed for some reason
            }

            throw new InvalidOperationException(
                $"'{fileName}' timed out after {timeout}.{Environment.NewLine}{output}");
        }

        // Wait (again, with no timeout) for the async output handlers to flush.
        process.WaitForExit();

        lock (output)
        {
            return (process.ExitCode, output.ToString());
        }
    }
}
