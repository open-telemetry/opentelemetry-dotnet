// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Classifies <see cref="IConfiguration"/> keys as owned by declarative configuration's strict
/// mode, so that <see cref="DeclarativeConfigurationProvider"/> can mask them from every source
/// registered earlier.
/// </summary>
internal static class StrictModeKeyScope
{
    /// <summary>
    /// Determines whether <paramref name="key"/> is an OTel setting that strict mode owns.
    /// </summary>
    /// <param name="key">The configuration key, as seen by <see cref="IConfigurationProvider"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the key's first segment starts with <c>OTEL_</c>, is not
    /// <c>OTEL_CONFIG_FILE</c>, and does not start with <c>OTEL_DOTNET_</c>.
    /// </returns>
    internal static bool IsInScope(string key)
    {
        var separatorIndex = key.IndexOf(ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
        var segment = key.AsSpan(0, separatorIndex < 0 ? key.Length : separatorIndex);

        return segment.StartsWith(OtelEnvironmentVariables.Prefix, StringComparison.OrdinalIgnoreCase)
            && !segment.Equals(OtelEnvironmentVariables.ConfigFile, StringComparison.OrdinalIgnoreCase)
            && !segment.StartsWith(OtelEnvironmentVariables.DotNetPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
