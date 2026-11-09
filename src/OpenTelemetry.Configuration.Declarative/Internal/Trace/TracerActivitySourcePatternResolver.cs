// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Resolves <c>tracer_configurator/development</c> into the <see cref="ActivitySource"/> name patterns to subscribe to.
/// </summary>
/// <remarks>
/// The SDK's <c>AddSource</c> API only adds subscriptions. All sources are subscribed to by
/// default; the configurator can narrow the set when <c>default_config.enabled</c> is false.
/// <para>
/// First-match precedence is applied only for entries with the same name: a later entry with a
/// name already used by an earlier entry is ignored. Because subscriptions cannot be subtracted,
/// an exclusion cannot be applied when <c>default_config.enabled</c> is true, or when a later
/// enabled entry could match the same sources. Both cases are reported by a warning.
/// </para>
/// </remarks>
internal static class TracerActivitySourcePatternResolver
{
    internal const string AllSourcesPattern = "*";

    /// <summary>
    /// Returns the source name patterns to subscribe to, and warns about disabled entries that cannot be honored.
    /// </summary>
    /// <remarks>
    /// Warnings consider only the document, not sources added in code.
    /// </remarks>
    /// <param name="configurator">The configurator, or <see langword="null"/> when omitted.</param>
    /// <param name="filePath">The document path, for diagnostics.</param>
    /// <returns>The source name patterns, which may contain <c>*</c> and <c>?</c> wildcards.</returns>
    internal static IReadOnlyList<string> Resolve(TracerConfiguratorSection? configurator, string filePath)
    {
        if (configurator is null)
        {
            return [AllSourcesPattern];
        }

        // Entries are matched first-wins, so a later entry with an already-used name can never apply.
        var uniqueEntries = configurator.Tracers
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        ReportUnappliedExclusions(uniqueEntries, configurator.DefaultEnabled, filePath);

        if (configurator.DefaultEnabled)
        {
            return [AllSourcesPattern];
        }

        return uniqueEntries
            .Where(entry => entry.Enabled)
            .Select(entry => entry.Name)
            .ToList()
            .AsReadOnly();
    }

    private static void ReportUnappliedExclusions(
        List<TracerConfiguratorEntry> entries,
        bool defaultEnabled,
        string filePath)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Enabled)
            {
                continue;
            }

            string? reason = null;
            if (defaultEnabled)
            {
                reason = $"'{YamlKeys.DefaultConfig}.{YamlKeys.Enabled}' is true and source subscriptions are additive";
            }
            else
            {
                for (var j = i + 1; j < entries.Count; j++)
                {
                    if (entries[j].Enabled && MayMatchSameSource(entry.Name, entries[j].Name))
                    {
                        reason = $"the later entry '{entries[j].Path}' (name '{entries[j].Name}') may enable matching tracers";
                        break;
                    }
                }
            }

            if (reason is not null)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.TracerExclusionNotApplied(
                    filePath,
                    entry.Path,
                    entry.Position.Line,
                    entry.Position.Column,
                    entry.Name,
                    reason);
            }
        }
    }

    // Two distinct names can only match the same source when at least one is a wildcard pattern.
    // Entries with identical names have already been collapsed, so this is deliberately
    // conservative: it may warn when the patterns turn out to be disjoint (for example "a*" and "b?").
    private static bool MayMatchSameSource(string first, string second) =>
        ContainsWildcard(first) || ContainsWildcard(second);

    private static bool ContainsWildcard(string pattern) =>
        pattern.AsSpan().IndexOfAny('*', '?') >= 0;
}
