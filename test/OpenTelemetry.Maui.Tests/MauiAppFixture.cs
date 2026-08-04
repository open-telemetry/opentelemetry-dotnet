// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Maui.Tests;

/// <summary>
/// Starts the in-process OTLP collector and then drives the MAUI test app on a
/// connected Android emulator. The device run is executed once for the whole
/// class: MAUI starts the app, which builds the OpenTelemetry providers from its
/// application host, and the on-device tests emit traces, metrics and logs over
/// OTLP/HTTP to the collector for the tests here to assert on.
/// </summary>
/// <remarks>
/// An Android emulator must already be running. The 'maui-android' workload is
/// required to build the app and 'adb' must be on the PATH (both are provided by
/// the CI workflow). The app is built and installed with 'dotnet build -t:Install'
/// and run with 'adb shell am instrument'; 'dotnet test' cannot drive on-device
/// Android in the current workload (its Microsoft.Testing.Platform pipe is not
/// reachable from the device).
/// </remarks>
public sealed class MauiAppFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private const string InstrumentationComponent =
        "io.opentelemetry.dotnet.maui/io.opentelemetry.dotnet.maui.TestInstrumentation";

    private static readonly TimeSpan BuildAndInstallTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan InstrumentationTimeout = TimeSpan.FromMinutes(5);

    internal OtlpHttpCollector Collector { get; private set; } = null!;

    internal int DeviceRunExitCode { get; private set; }

    internal string DeviceRunOutput { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Bind on all interfaces so the emulator can reach the collector via
        // 10.0.2.2 (the alias for the host loopback).
        this.Collector = await OtlpHttpCollector.StartAsync("http://0.0.0.0:4318");

        var repoRoot = RepoRoot();
        var project = Path.Combine(repoRoot, "test", "OpenTelemetry.Maui.TestApp", "OpenTelemetry.Maui.TestApp.csproj");

        // Build the APK and install it on the connected emulator.
        var (installExitCode, installOutput) = RunProcess(
            "dotnet",
            ["build", project, "--configuration", Configuration, "--framework", AndroidTargetFramework(), "-t:Install"],
            repoRoot,
            BuildAndInstallTimeout);

        if (installExitCode != 0)
        {
            this.DeviceRunExitCode = installExitCode;
            this.DeviceRunOutput = "APK build/install failed." + Environment.NewLine + installOutput;
            return;
        }

        // Run the on-device instrumentation synchronously. It executes the tests via
        // Microsoft.Testing.Platform in the running MAUI app, which export OTLP to
        // the collector.
        var (runExitCode, runOutput) = RunProcess(
            "adb",
            ["shell", "am", "instrument", "-w", InstrumentationComponent],
            repoRoot,
            InstrumentationTimeout);

        // 'am instrument' exits 0 even when tests fail; success is signalled by the
        // instrumentation result: Result.Ok (INSTRUMENTATION_CODE: -1) with failed=0.
        // A run that never got as far as discovering any tests - because MAUI failed
        // to start the app, for example - also reports failed=0, so at least one
        // test must have passed for the run to count as a success.
        var succeeded =
            runExitCode == 0 &&
            runOutput.Contains("INSTRUMENTATION_CODE: -1", StringComparison.Ordinal) &&
            ReportedCount(runOutput, "failed") == 0 &&
            ReportedCount(runOutput, "passed") > 0;

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

    /// <summary>
    /// Reads a count out of the instrumentation results, which 'am instrument -w'
    /// prints as lines of the form 'INSTRUMENTATION_RESULT: passed=3'.
    /// </summary>
    private static int ReportedCount(string output, string name)
    {
        var match = Regex.Match(
            output,
            @"^INSTRUMENTATION_RESULT:\s*" + Regex.Escape(name) + @"=(?<count>\d+)\s*$",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        return match.Success
            ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture)
            : 0;
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
