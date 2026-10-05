// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Reports, once per registration, the OpenTelemetry settings that strict mode ignores and the
/// configuration sources that strict mode cannot mask.
/// </summary>
/// <remarks>
/// Runs when the first OpenTelemetry provider is built, so that it sees the application's final
/// configuration, including sources added after <c>UseDeclarativeConfiguration</c> was called.
/// Only key names are reported, never values.
/// </remarks>
internal sealed class StrictModeDiagnostics
{
    private int reported;

    /// <summary>
    /// Evaluates the diagnostics against <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    internal static void Report(IConfiguration? configuration) => Report(configuration, expectedFilePath: null);

    /// <summary>
    /// Evaluates the diagnostics against the <see cref="IConfiguration"/> registered in
    /// <paramref name="serviceProvider"/>, the first time it is called.
    /// </summary>
    /// <param name="serviceProvider">The application's service provider.</param>
    internal void ReportOnce(IServiceProvider serviceProvider)
    {
        if (Interlocked.Exchange(ref this.reported, 1) != 0)
        {
            return;
        }

        var configuration = serviceProvider.GetService<IConfiguration>();
        var expectedFilePath = serviceProvider
            .GetService<DeclarativeConfigurationDocumentAccessor>()
            ?.FilePath.DisplayPath;

        Report(configuration, expectedFilePath);
    }

    private static void Report(IConfiguration? configuration, string? expectedFilePath)
    {
        var analysis = StrictModeDiagnosticsAnalyzer.Analyze(configuration);
        if (analysis.Failure != StrictModeDiagnosticsAnalyzer.Failure.None)
        {
            if (analysis.Failure == StrictModeDiagnosticsAnalyzer.Failure.DeclarativeProviderNotFound
                && expectedFilePath != null)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.StrictModeConfigurationSourceUnreachable(
                    expectedFilePath);
            }
            else
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.StrictModeDiagnosticsUnavailable(
                    analysis.UnavailableReason!);
            }

            return;
        }

        if (analysis.IgnoredKeys.Count > 0)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.StrictModeSettingsIgnored(
                analysis.FilePath!,
                string.Join(", ", analysis.IgnoredKeys));
        }

        foreach (var provider in analysis.UnmaskedProviders)
        {
            if (provider.IsEnvironmentVariables)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.LaterEnvironmentVariablesOverrideStrictMode(
                    analysis.FilePath!,
                    string.Join(", ", provider.Keys));
            }
            else
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.LaterSourceOverridesStrictMode(
                    analysis.FilePath!,
                    provider.ProviderType,
                    string.Join(", ", provider.Keys));
            }
        }
    }
}
