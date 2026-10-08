// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
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
    /// The configuration paths the SDK binds into options. Each is masked as a whole path: the key
    /// equals the path or sits beneath it, so settings applications define elsewhere under
    /// <c>OpenTelemetry</c> stay visible. Only paths the SDK currently binds are listed.
    /// </summary>
#if NET
    private static readonly FrozenSet<string> ReservedSectionPaths = FrozenSet.ToFrozenSet(
        ["OpenTelemetry:Exporters:Console"],
        StringComparer.OrdinalIgnoreCase);
#else
    private static readonly HashSet<string> ReservedSectionPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "OpenTelemetry:Exporters:Console",
    };
#endif

    /// <summary>
    /// Determines whether <paramref name="key"/> is an OTel setting that strict mode owns.
    /// </summary>
    /// <param name="key">The configuration key, as seen by <see cref="IConfigurationProvider"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the key is a reserved SDK section path or beneath one, or when
    /// its first segment starts with <c>OTEL_</c>, is not <c>OTEL_CONFIG_FILE</c>, and does not
    /// start with <c>OTEL_DOTNET_</c>.
    /// </returns>
    internal static bool IsInScope(string key)
    {
        foreach (var path in ReservedSectionPaths)
        {
            if (IsPathOrDescendant(key, path))
            {
                return true;
            }
        }

        var separatorIndex = key.IndexOf(ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
        var segment = key.AsSpan(0, separatorIndex < 0 ? key.Length : separatorIndex);

        return segment.StartsWith(OtelEnvironmentVariables.Prefix, StringComparison.OrdinalIgnoreCase)
            && !segment.Equals(OtelEnvironmentVariables.ConfigFile, StringComparison.OrdinalIgnoreCase)
            && !segment.StartsWith(OtelEnvironmentVariables.DotNetPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathOrDescendant(string key, string path) =>
        key.StartsWith(path, StringComparison.OrdinalIgnoreCase) &&
        (key.Length == path.Length || key[path.Length] == ':');
}
