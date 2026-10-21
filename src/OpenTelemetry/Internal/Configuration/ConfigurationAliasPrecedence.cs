// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Internal;

/// <summary>
/// Chooses between an <c>OTEL_*</c> variable and the configuration section key for the same
/// option by provider order, as .NET chooses between providers for a single key: the value from
/// the later provider wins. When both come from the same provider, or the configuration does not
/// expose its providers, the section key wins.
/// </summary>
internal static class ConfigurationAliasPrecedence
{
    /// <summary>
    /// Creates the section to bind, without excluded keys or keys whose variable comes from a later
    /// provider, and with values converted by <see cref="ConfigurationAlias.NormalizeSectionValue"/>.
    /// </summary>
    /// <remarks>
    /// A variable the options type did not apply, because it is unset, blank, or unrecognised,
    /// never overrides the section. A section value that loses is not bound at all, so it cannot
    /// fail binding. Keys are removed with their children. The result is a snapshot. Call this
    /// method again when the original configuration reloads.
    /// </remarks>
    /// <param name="configuration">The configuration the section and variables are read from.</param>
    /// <param name="sectionPath">The path of the section to bind.</param>
    /// <param name="aliases">The environment variables that set the same options as section keys.</param>
    /// <param name="isVariableApplied">Whether the options type applied the named variable.</param>
    /// <param name="excludedKeys">The keys, relative to the section, that the caller resolves itself.</param>
    /// <returns>A snapshot of the filtered and normalized section.</returns>
    public static IConfiguration CreateBindableSection(
        IConfiguration configuration,
        string sectionPath,
        IReadOnlyList<ConfigurationAlias> aliases,
        Func<string, bool> isVariableApplied,
        IEnumerable<string> excludedKeys)
    {
        var providers = GetProviders(configuration);

        // First decide which top-level keys to leave out: the caller's own keys, and every key
        // whose environment variable wins.
        var omittedKeys = new HashSet<string>(excludedKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            if (IsOverriddenByEnvironmentVariable(configuration, sectionPath, alias, isVariableApplied, providers))
            {
                omittedKeys.Add(alias.SectionKey);
            }
        }

        // Then copy everything else, normalized so the binder accepts the values the variables accept.
        List<KeyValuePair<string, string?>> bindableValues = [];
        foreach (var pair in configuration.GetSection(sectionPath).AsEnumerable(makePathsRelative: true))
        {
            if (pair.Value is not { } value || omittedKeys.Contains(GetFirstSegment(pair.Key)))
            {
                continue;
            }

            // The copy keeps the full key paths, so a binding error still names the original key.
            bindableValues.Add(new(ConfigurationPath.Combine(sectionPath, pair.Key), NormalizeSectionValue(aliases, pair.Key, value)));
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(bindableValues)
            .Build()
            .GetSection(sectionPath);
    }

    /// <summary>
    /// Determines whether the value of <paramref name="key"/> comes from a later provider than the
    /// value of <paramref name="otherKey"/>.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when both come from the same provider or the configuration
    /// does not expose its providers, so a caller that lets <paramref name="otherKey"/> win on
    /// <see langword="false"/> resolves ties as <see cref="CreateBindableSection"/> does. A key no
    /// provider sets counts as coming before every provider.
    /// </remarks>
    /// <param name="configuration">The configuration both keys are read from.</param>
    /// <param name="key">The key that must come from the later provider.</param>
    /// <param name="otherKey">The key it is compared with.</param>
    /// <returns><see langword="true"/> if <paramref name="key"/> comes from a later provider; otherwise <see langword="false"/>.</returns>
    public static bool IsFromLaterProvider(IConfiguration configuration, string key, string otherKey)
        => IsFromLaterProvider(GetProviders(configuration), key, otherKey);

    private static IReadOnlyList<IConfigurationProvider>? GetProviders(IConfiguration configuration)
        => configuration is IConfigurationRoot root ? [.. root.Providers] : null;

    private static bool IsFromLaterProvider(IReadOnlyList<IConfigurationProvider>? providers, string key, string otherKey)
        => providers != null
            && GetWinningProviderIndex(providers, key) > GetWinningProviderIndex(providers, otherKey);

    private static bool IsOverriddenByEnvironmentVariable(
        IConfiguration configuration,
        string sectionPath,
        ConfigurationAlias alias,
        Func<string, bool> isVariableApplied,
        IReadOnlyList<IConfigurationProvider>? providers)
    {
        var sectionKey = ConfigurationPath.Combine(sectionPath, alias.SectionKey);

        return configuration[sectionKey] != null
            && isVariableApplied(alias.VariableName)
            && IsFromLaterProvider(providers, alias.VariableName, sectionKey);
    }

    private static string NormalizeSectionValue(IReadOnlyList<ConfigurationAlias> aliases, string relativeKey, string value)
    {
        foreach (var alias in aliases)
        {
            if (alias.NormalizeSectionValue != null
                && string.Equals(alias.SectionKey, relativeKey, StringComparison.OrdinalIgnoreCase))
            {
                return alias.NormalizeSectionValue(value);
            }
        }

        return value;
    }

    // Must match the ConfigurationRoot indexer: the last provider whose TryGet succeeds supplies
    // the value, even when that value is null.
    private static int GetWinningProviderIndex(IReadOnlyList<IConfigurationProvider> providers, string key)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            if (providers[i].TryGet(key, out _))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetFirstSegment(string relativeKey)
    {
        var separatorIndex = relativeKey.IndexOf(ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
        return separatorIndex < 0 ? relativeKey : relativeKey.Substring(0, separatorIndex);
    }
}
