// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Text.Json.Nodes;
#else
using System.Xml;
#endif

namespace OpenTelemetry.Tests;

public class BaggageConfigurationTests
{
    private const string AppContextSwitch = "OpenTelemetry.Baggage.AllowUnsafeLegacyFlow";

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public Task FlowModeIsSelectedAtProcessStartup(bool? enabled)
        => RunChildProcess(enabled);

    private static async Task RunChildProcess(bool? appContextValue)
    {
        using var directory = new TemporaryDirectory();
        await RunChildProcess(CreateStartInfo(directory.Path, appContextValue), appContextValue);
    }

    private static async Task RunChildProcess(ProcessStartInfo startInfo, bool? appContextValue)
    {
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
#if NET
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
#else
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
#endif

        try
        {
#if NET
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
#else
            Assert.True(process.WaitForExit(30_000), "Baggage child test process timed out.");
#endif
            Assert.True(
                process.ExitCode == 0,
                $"Baggage AppContext '{appContextValue?.ToString() ?? "<unset>"}' failed:\n{await output}\n{await error}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
#if NET
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(timeout.Token);
#else
                Assert.True(process.WaitForExit(5_000), "Baggage child test process did not exit after termination.");
#endif
            }
        }
    }

    private static ProcessStartInfo CreateStartInfo(string directory, bool? enabled)
    {
        var assemblyPath = typeof(BaggageTests).Assembly.Location;
#if NET
        var config = JsonNode.Parse(File.ReadAllText(Path.ChangeExtension(assemblyPath, ".runtimeconfig.json")))!;
        var options = config["runtimeOptions"]!;
        var properties = options["configProperties"]?.AsObject();
        if (properties == null)
        {
            properties = [];
            options["configProperties"] = properties;
        }

        properties.Remove(AppContextSwitch);
        if (enabled.HasValue)
        {
            properties[AppContextSwitch] = enabled.Value;
        }

        var configPath = Path.Combine(directory, "test.runtimeconfig.json");
        File.WriteAllText(configPath, config.ToJsonString());
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(configPath);
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("--filter-class");
        startInfo.ArgumentList.Add(typeof(BaggageTests).FullName!);
#else
        // Framework loads configuration next to the executable. Give the child
        // its own copy without changing the parent's executable or configuration.
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)!))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        var childAssemblyPath = Path.Combine(directory, Path.GetFileName(assemblyPath));
        var configPath = childAssemblyPath + ".config";
        var config = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(configPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            config.Load(reader);
        }

        var runtime = config.SelectSingleNode("/configuration/runtime")!;
        var overrides = (XmlElement?)runtime.SelectSingleNode("AppContextSwitchOverrides");
        if (overrides == null)
        {
            overrides = config.CreateElement("AppContextSwitchOverrides");
            runtime.AppendChild(overrides);
        }

        var switches = overrides.GetAttribute("value").Split(';')
            .Where(value => !string.IsNullOrWhiteSpace(value)
                && !value.StartsWith(AppContextSwitch + "=", StringComparison.Ordinal))
            .ToList();
        if (enabled.HasValue)
        {
            switches.Add($"{AppContextSwitch}={enabled.Value}");
        }

        if (switches.Count == 0)
        {
            runtime.RemoveChild(overrides);
        }
        else
        {
            overrides.SetAttribute("value", string.Join(";", switches));
        }

        config.Save(configPath);
        var startInfo = new ProcessStartInfo(childAssemblyPath)
        {
            Arguments = $"--filter-class {typeof(BaggageTests).FullName}",
        };
#endif
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }
}
