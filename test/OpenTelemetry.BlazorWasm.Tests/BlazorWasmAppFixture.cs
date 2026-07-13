// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace OpenTelemetry.BlazorWasm.Tests;

public sealed class BlazorWasmAppFixture : IAsyncLifetime
{
    private string? publishDirectory;

    internal OtlpHttpCollector Collector { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        InstallPlaywright();

        this.publishDirectory = PublishClient();
        this.Collector = await OtlpHttpCollector.StartAsync(Path.Combine(this.publishDirectory, "wwwroot"));
    }

    public async Task DisposeAsync()
    {
        if (this.Collector is not null)
        {
            await this.Collector.DisposeAsync();
        }

        if (this.publishDirectory is not null && Directory.Exists(this.publishDirectory))
        {
            try
            {
                Directory.Delete(this.publishDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup of the temporary publish output.
            }
        }
    }

    private static void InstallPlaywright()
    {
        string[] arguments = OperatingSystem.IsLinux()
            ? ["install", "chromium", "--with-deps"]
            : ["install", "chromium"];

        var exitCode = Microsoft.Playwright.Program.Main(arguments);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser install exited with code {exitCode}.");
        }
    }

    private static string CurrentTargetFramework()
    {
        // Derive the moniker (e.g. "net10.0") from the framework the tests were
        // built against so the published client matches the current test TFM.
        var frameworkName = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        if (frameworkName is not null)
        {
            const string Marker = "Version=v";
            var index = frameworkName.IndexOf(Marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                return $"net{frameworkName[(index + Marker.Length)..]}";
            }
        }

        var version = Environment.Version;
        return $"net{version.Major}.{version.Minor}";
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

    private static string PublishClient()
    {
        var repoRoot = RepoRoot();
        var project = Path.Combine(repoRoot, "test", "OpenTelemetry.BlazorWasm.TestApp", "OpenTelemetry.BlazorWasm.TestApp.csproj");
        var output = Path.Combine(Path.GetTempPath(), "otel-blazor-wasm-" + Guid.NewGuid().ToString("N"));

#if DEBUG
        const string Configuration = "Debug";
#else
        const string Configuration = "Release";
#endif

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repoRoot,
        };

        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(Configuration);

        // Publish the client for the same target framework the test is running
        // under so the suite works unchanged when run against multiple TFMs.
        startInfo.ArgumentList.Add("--framework");
        startInfo.ArgumentList.Add(CurrentTargetFramework());
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(output);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start 'dotnet publish' for the Blazor client.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet publish' failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        return output;
    }
}
