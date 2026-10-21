// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.AotCompatibility.TestApp;

internal static class SelfDiagnosticsConfigurationTests
{
    public static void Run()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "info",
                ["OpenTelemetry:SelfDiagnostics:EnvironmentVariables"] = "all",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetry().WithTracing();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();
        VerifyOptions(monitor.CurrentValue, LogLevel.Information, EnvironmentVariableLogMode.AllValues);

        configuration["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "warn";
        configuration.Reload();
        VerifyOptions(monitor.CurrentValue, LogLevel.Warning, EnvironmentVariableLogMode.AllValues);
    }

    private static void VerifyOptions(SelfDiagnosticsOptions options, LogLevel level, EnvironmentVariableLogMode mode)
    {
        if (options.MinimumLevel != level || options.EnvironmentVariables != mode)
        {
            throw new InvalidOperationException("Automatic self-diagnostics configuration failed.");
        }
    }
}
