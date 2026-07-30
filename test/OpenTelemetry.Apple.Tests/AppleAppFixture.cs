// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Apple.Tests;

/// <summary>
/// Starts the in-process OTLP collector and then drives the iOS test app on a
/// simulator. The device run is executed once for the whole class: the app emits
/// traces, metrics and logs over OTLP/HTTP to the collector and the tests then
/// assert on what was received.
/// </summary>
/// <remarks>
/// Requires macOS with Xcode (for <c>xcrun simctl</c>) and the .NET 'ios'
/// workload. An iOS simulator is booted on demand if one is not already running.
/// The app is built with 'dotnet build', installed with 'xcrun simctl install'
/// and run with 'xcrun simctl launch'; 'dotnet test' cannot drive on-device iOS
/// in the current workload (its Microsoft.Testing.Platform pipe is not reachable
/// from the simulator).
/// </remarks>
public sealed class AppleAppFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private const string BundleIdentifier = "io.opentelemetry.dotnet.apple";
    private const string ResultsDirectoryName = "TestResults";
    private const string SummaryFileName = "summary.txt";

    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SimulatorBootTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SimulatorCommandTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TestRunTimeout = TimeSpan.FromMinutes(5);

    private readonly StringBuilder log = new();

    private string? simulatorBootedByFixture;

    internal OtlpHttpCollector Collector { get; private set; } = null!;

    internal int DeviceRunExitCode { get; private set; }

    internal string DeviceRunOutput
    {
        get => (field is null ? string.Empty : field + Environment.NewLine) + this.log.ToString();
        private set;
    }

    public async Task InitializeAsync()
    {
        this.Collector = await OtlpHttpCollector.StartAsync();

        try
        {
            this.RunOnSimulator();
        }
        catch (Exception ex)
        {
            this.Fail(ex.ToString());
        }
    }

    public async Task DisposeAsync()
    {
        if (this.simulatorBootedByFixture is not null)
        {
            try
            {
                RunProcess("xcrun", ["simctl", "shutdown", this.simulatorBootedByFixture], null, SimulatorCommandTimeout);
            }
            catch (Exception)
            {
                // Best effort shutdown of the simulator the fixture booted.
            }
        }

        if (this.Collector is not null)
        {
            await this.Collector.DisposeAsync();
        }
    }

    private static string AppleTargetFramework()
    {
        var frameworkName = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        if (frameworkName is not null)
        {
            const string Marker = "Version=v";
            var index = frameworkName.IndexOf(Marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                return $"net{frameworkName[(index + Marker.Length)..]}-ios";
            }
        }

        return $"net{Environment.Version.ToString(2)}-ios";
    }

    private static string SimulatorRuntimeIdentifier() =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "iossimulator-arm64"
            : "iossimulator-x64";

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

    private static string FindAppBundle(string repoRoot, string projectPath, string runtimeIdentifier)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

        // The repository builds with UseArtifactsOutput so the bundle lands under
        // artifacts/bin; the per-project bin directory is checked as a fallback in
        // case the output layout changes.
        string[] roots =
        [
            Path.Combine(repoRoot, "artifacts", "bin", projectName),
            Path.Combine(Path.GetDirectoryName(projectPath)!, "bin"),
        ];

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var bundle = Directory.EnumerateDirectories(root, "*.app", SearchOption.AllDirectories)
                .Where((p) => p.Contains(runtimeIdentifier, StringComparison.Ordinal))
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (bundle is not null)
            {
                return bundle;
            }
        }

        throw new InvalidOperationException(
            $"Could not find a built '{projectName}' app bundle for '{runtimeIdentifier}' below: {string.Join(", ", roots)}.");
    }

    private static int ReadCount(string summary, string key)
    {
        foreach (var line in summary.Split('\n'))
        {
            var value = line.Trim();

            if (value.StartsWith(key + "=", StringComparison.Ordinal) &&
                int.TryParse(value[(key.Length + 1)..], CultureInfo.InvariantCulture, out var count))
            {
                return count;
            }
        }

        return -1;
    }

    private static void TryCopyDirectory(string source, string destination)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                File.Copy(file, target, overwrite: true);
            }
        }
        catch (Exception)
        {
            // Best effort copy of the on-device results for the CI artifacts.
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProcess(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Stop the persistent build servers from inheriting the redirected handles,
        // otherwise WaitForExit can block on a build server that idle-times-out long
        // after the command itself finished.
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (standardOutput)
                {
                    standardOutput.AppendLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (standardError)
                {
                    standardError.AppendLine(e.Data);
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
                $"'{fileName}' timed out after {timeout}.{Environment.NewLine}{standardOutput}{standardError}");
        }

        // Wait (again, with no timeout) for the async output handlers to flush.
        process.WaitForExit();

        lock (standardOutput)
        {
            lock (standardError)
            {
                return (process.ExitCode, standardOutput.ToString(), standardError.ToString());
            }
        }
    }

    private void RunOnSimulator()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "The iOS end-to-end tests require macOS with Xcode and the .NET 'ios' workload installed.");
        }

        var repoRoot = RepoRoot();
        var project = Path.Combine(repoRoot, "test", "OpenTelemetry.Apple.TestApp", "OpenTelemetry.Apple.TestApp.csproj");
        var runtimeIdentifier = SimulatorRuntimeIdentifier();

        // Build the app bundle for the simulator matching the host architecture.
        var (buildExitCode, _, _) = this.Run(
            "dotnet",
            [
                "build",
                project,
                "--configuration",
                Configuration,
                "--framework",
                AppleTargetFramework(),
                "-p:RuntimeIdentifier=" + runtimeIdentifier
            ],
            repoRoot,
            BuildTimeout);

        if (buildExitCode != 0)
        {
            this.Fail($"The iOS app failed to build with exit code {buildExitCode}.");
            return;
        }

        var appBundle = FindAppBundle(repoRoot, project, runtimeIdentifier);
        var (simulator, alreadyBooted) = this.ResolveSimulator();

        if (!alreadyBooted)
        {
            // 'bootstatus -b' boots the simulator if required, then waits for the boot to complete
            var (bootExitCode, _, _) = this.Run("xcrun", ["simctl", "bootstatus", simulator, "-b"], repoRoot, SimulatorBootTimeout);

            if (bootExitCode != 0)
            {
                this.Fail($"Failed to boot the iOS simulator '{simulator}' with exit code {bootExitCode}.");
                return;
            }

            this.simulatorBootedByFixture = simulator;
        }

        // Remove any previously installed copy so that the app's data container -
        // where the on-device run writes its results - starts out empty.
        this.Run("xcrun", ["simctl", "uninstall", simulator, BundleIdentifier], repoRoot, SimulatorCommandTimeout);

        var (installExitCode, _, _) = this.Run("xcrun", ["simctl", "install", simulator, appBundle], repoRoot, SimulatorCommandTimeout);

        if (installExitCode != 0)
        {
            this.Fail($"Failed to install the iOS app on the simulator with exit code {installExitCode}.");
            return;
        }

        // Environment variables prefixed with SIMCTL_CHILD_ are passed through to
        // the launched app with the prefix removed. This is how the app is told
        // which port the collector on the host is listening on.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SIMCTL_CHILD_OTEL_TEST_OTLP_ENDPOINT"] = this.Collector.BaseUrl,
        };

        // '--console-pty' streams the app's stdout and stderr and blocks until the
        // app exits, which is when the on-device test run has finished.
        var (runExitCode, _, _) = this.Run(
            "xcrun",
            ["simctl", "launch", "--console-pty", "--terminate-running-process", simulator, BundleIdentifier],
            repoRoot,
            TestRunTimeout,
            environment);

        var results = this.CollectResults(simulator);

        if (results is null)
        {
            // 'simctl launch' reports its own success, not the app's exit code, so
            // a missing summary means the app failed to start or crashed.
            this.Fail($"The on-device test run did not write a results summary ('simctl launch' exited with code {runExitCode}).");
            return;
        }

        var (passed, failed, skipped) = results.Value;

        if (failed != 0 || passed <= 0)
        {
            this.Fail($"The on-device test run reported {passed} passed, {failed} failed and {skipped} skipped test(s).");
            return;
        }

        this.DeviceRunExitCode = 0;
    }

    private (string Simulator, bool AlreadyBooted) ResolveSimulator()
    {
        var (exitCode, listOutput, _) = this.Run("xcrun", ["simctl", "list", "devices", "available", "--json"], null, SimulatorCommandTimeout);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"'xcrun simctl list devices' failed with exit code {exitCode}.");
        }

        // Runtime keys look like 'com.apple.CoreSimulator.SimRuntime.iOS-18-2'.
        const string RuntimePrefix = "com.apple.CoreSimulator.SimRuntime.iOS-";

        (Version Runtime, string Udid, bool Booted)? candidate = null;

        using var document = JsonDocument.Parse(listOutput);

        foreach (var runtime in document.RootElement.GetProperty("devices").EnumerateObject())
        {
            if (!runtime.Name.StartsWith(RuntimePrefix, StringComparison.Ordinal) ||
                !Version.TryParse(runtime.Name[RuntimePrefix.Length..].Replace('-', '.'), out var version))
            {
                continue;
            }

            foreach (var device in runtime.Value.EnumerateArray())
            {
                if (device.TryGetProperty("isAvailable", out var isAvailable) && !isAvailable.GetBoolean())
                {
                    continue;
                }

                var name = device.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                var udid = device.TryGetProperty("udid", out var udidProperty) ? udidProperty.GetString() : null;

                if (udid is null || name is null || !name.StartsWith("iPhone", StringComparison.Ordinal))
                {
                    continue;
                }

                var booted = string.Equals(
                    device.TryGetProperty("state", out var state) ? state.GetString() : null,
                    "Booted",
                    StringComparison.Ordinal);

                // Prefer a simulator that is already running, otherwise the device
                // with the most recent iOS runtime.
                if (candidate is null ||
                    (booted && !candidate.Value.Booted) ||
                    (booted == candidate.Value.Booted && version > candidate.Value.Runtime))
                {
                    candidate = (version, udid, booted);
                }
            }
        }

        if (candidate is null)
        {
            throw new InvalidOperationException(
                "No available iPhone simulator was found. Install an iOS simulator runtime with Xcode.");
        }

        return (candidate.Value.Udid, candidate.Value.Booted);
    }

    private (int Passed, int Failed, int Skipped)? CollectResults(string simulator)
    {
        var (exitCode, standardOutput, _) = this.Run(
            "xcrun",
            ["simctl", "get_app_container", simulator, BundleIdentifier, "data"],
            null,
            SimulatorCommandTimeout);

        if (exitCode != 0)
        {
            return null;
        }

        var resultsDirectory = Path.Combine(standardOutput.Trim(), "Documents", ResultsDirectoryName);

        if (!Directory.Exists(resultsDirectory))
        {
            return null;
        }

        // Copy the results written on the device (the TRX report and any failure
        // details) to the host so that CI can upload them as artifacts.
        TryCopyDirectory(resultsDirectory, Path.Combine(AppContext.BaseDirectory, ResultsDirectoryName, "apple-device"));

        var summaryFile = Path.Combine(resultsDirectory, SummaryFileName);

        if (!File.Exists(summaryFile))
        {
            return null;
        }

        var summary = File.ReadAllText(summaryFile);

        this.log.AppendLine(summary);

        return (ReadCount(summary, "passed"), ReadCount(summary, "failed"), ReadCount(summary, "skipped"));
    }

    private (int ExitCode, string StandardOutput, string StandardError) Run(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        this.log.AppendLine("$ " + fileName + " " + string.Join(' ', arguments));

        var result = RunProcess(fileName, arguments, workingDirectory, timeout, environment);

        this.log.Append(result.StandardOutput).Append(result.StandardError);

        return result;
    }

    private void Fail(string reason)
    {
        this.DeviceRunExitCode = 1;
        this.DeviceRunOutput = reason;
    }
}
