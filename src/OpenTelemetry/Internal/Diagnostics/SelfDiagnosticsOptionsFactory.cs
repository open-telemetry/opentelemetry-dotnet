// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Creates the default <see cref="SelfDiagnosticsOptions"/> from <c>OTEL_*</c> environment variables and
/// the <c>OpenTelemetry:SelfDiagnostics</c> configuration section.
/// </summary>
internal static class SelfDiagnosticsOptionsFactory
{
    /// <summary>
    /// Creates the options from environment-variable defaults, then binds the section keys that win over them.
    /// </summary>
    /// <param name="configuration">The configuration the environment variables and section are read from.</param>
    /// <param name="defaultLogDirectoryResolver">Resolves the default log directory; defaults to <see cref="SelfDiagnosticsOptions.GetDefaultLogDirectory"/>.</param>
    /// <param name="reportConfigurationError">Reports a configuration problem when the file sink cannot start.</param>
    /// <returns>The options.</returns>
    /// <exception cref="InvalidOperationException">A winning section value cannot be bound.</exception>
    internal static SelfDiagnosticsOptions CreateDefault(
        IConfiguration configuration,
        Func<string?>? defaultLogDirectoryResolver = null,
        Action<string>? reportConfigurationError = null)
    {
        var appliedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new SelfDiagnosticsOptions(
            configuration,
            defaultLogDirectoryResolver ?? SelfDiagnosticsOptions.GetDefaultLogDirectory,
            reportConfigurationError,
            configuration.GetSection(SelfDiagnosticsOptions.ConfigurationSectionPath),
            appliedVariables);

        ConfigurationAliasPrecedence.CreateBindableSection(
            configuration,
            SelfDiagnosticsOptions.ConfigurationSectionPath,
            SelfDiagnosticsOptionsEnvironmentDefaults.SectionKeyAliases,
            appliedVariables.Contains,
            SelfDiagnosticsOptionsEnvironmentDefaults.SectionKeysResolvedWithVariables).Bind(options);

        return options;
    }
}
