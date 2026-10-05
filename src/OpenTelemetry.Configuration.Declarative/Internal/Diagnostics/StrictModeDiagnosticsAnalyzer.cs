// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Analyzes how strict mode interacts with the application's configuration provider graph.
/// </summary>
internal static class StrictModeDiagnosticsAnalyzer
{
    internal enum Failure
    {
        /// <summary>
        /// The analysis completed successfully.
        /// </summary>
        None,

        /// <summary>
        /// The registered configuration does not expose its provider graph.
        /// </summary>
        ConfigurationIsNotRoot,

        /// <summary>
        /// No declarative provider is reachable from the registered configuration.
        /// </summary>
        DeclarativeProviderNotFound,
    }

    internal static Analysis Analyze(IConfiguration? configuration)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return Analysis.Unavailable(
                Failure.ConfigurationIsNotRoot,
                $"the application's {nameof(IConfiguration)} is not an {nameof(IConfigurationRoot)}");
        }

        var entries = new List<ProviderEntry>();
        Flatten(root, entries);

        var declarativeIndex = entries.FindLastIndex(entry => entry.Provider is DeclarativeConfigurationProvider);
        if (declarativeIndex < 0)
        {
            return Analysis.Unavailable(
                Failure.DeclarativeProviderNotFound,
                $"no declarative configuration provider is reachable from the application's {nameof(IConfiguration)}");
        }

        var declarativeEntry = entries[declarativeIndex];
        var declarative = (DeclarativeConfigurationProvider)declarativeEntry.Provider;
        var masked = FindMaskedEntries(entries, declarativeIndex, declarativeEntry);

        return Analysis.Available(
            declarative.FilePath.DisplayPath,
            FindIgnoredKeys(entries, masked, declarativeEntry, declarative),
            FindUnmaskedProviders(entries, masked, declarativeEntry, declarativeIndex));
    }

    private static HashSet<ProviderEntry> FindMaskedEntries(
        List<ProviderEntry> entries,
        int declarativeIndex,
        ProviderEntry declarativeEntry)
    {
        var masked = new HashSet<ProviderEntry>();

        // The mask applies only within the configuration root that holds the declarative provider.
        // A ChainedConfigurationProvider reports a masked null or empty value as not found, so
        // providers ahead of the chain in an enclosing root remain outside the mask.
        foreach (var entry in entries.Take(declarativeIndex))
        {
            if (entry.IsWithinSameOwnerOccurrence(declarativeEntry))
            {
                masked.Add(entry);
            }
        }

        return masked;
    }

    private static SortedSet<string> FindIgnoredKeys(
        List<ProviderEntry> entries,
        HashSet<ProviderEntry> masked,
        ProviderEntry declarativeEntry,
        DeclarativeConfigurationProvider declarative)
    {
        var document = declarative.Accessor.GetDocument();
        var ignored = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!masked.Contains(entry) || entry.Provider is DeclarativeConfigurationProvider)
            {
                continue;
            }

            foreach (var key in GetInScopeKeys(entry.Provider))
            {
                if (!entry.IsExposedTo(declarativeEntry, key))
                {
                    continue;
                }

                if (!IsImportedEnvironmentVariable(entry.Provider, key, document))
                {
                    ignored.Add(key);
                }
            }
        }

        return ignored;
    }

    private static List<UnmaskedProvider> FindUnmaskedProviders(
        List<ProviderEntry> entries,
        HashSet<ProviderEntry> masked,
        ProviderEntry declarativeEntry,
        int declarativeIndex)
    {
        var providers = new List<UnmaskedProvider>();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];

            // Other declarative providers are reported by MultipleConfigurationDocumentsReachable.
            if (masked.Contains(entry) || entry.Provider is DeclarativeConfigurationProvider)
            {
                continue;
            }

            var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in GetInScopeKeys(entry.Provider, includeEmptyValues: true))
            {
                if (!entry.IsExposedThroughContainingChains(key))
                {
                    continue;
                }

                if (index < declarativeIndex
                    && IsKeyOverriddenByDeclarativeRoute(entry, declarativeEntry, key))
                {
                    continue;
                }

                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                providers.Add(new UnmaskedProvider(entry.Provider, keys));
            }
        }

        return providers;
    }

    /// <summary>
    /// Determines whether an environment variables provider's value for a key is the value the
    /// document imports. Substitution of <c>${KEY}</c> reads the unprefixed process variable, so a
    /// provider value is only imported when it matches the value the document resolved for that
    /// variable. A prefixed provider that exposes <c>APP_KEY</c> as <c>KEY</c> therefore does not
    /// count unless both hold the same value, in which case nothing is masked.
    /// </summary>
    /// <param name="provider">The provider to inspect.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="document">The declarative configuration document.</param>
    /// <returns><see langword="true"/> if the document imports the provider's value.</returns>
    private static bool IsImportedEnvironmentVariable(
        IConfigurationProvider provider,
        string key,
        DeclarativeConfigurationDocument document) =>
            provider is EnvironmentVariablesConfigurationProvider
            && document.TryGetReferencedEnvironmentVariable(key, out var importedValue)
            && provider.TryGet(key, out var providerValue)
            && string.Equals(providerValue, importedValue, StringComparison.Ordinal);

    // Returns the first-segment keys in scope for strict mode that the provider sets.
    private static IEnumerable<string> GetInScopeKeys(
        IConfigurationProvider provider,
        bool includeEmptyValues = false)
    {
        foreach (var segment in provider.GetChildKeys([], parentPath: null).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!StrictModeKeyScope.IsInScope(segment))
            {
                continue;
            }

            if ((provider.TryGet(segment, out var value) && (includeEmptyValues || !string.IsNullOrEmpty(value)))
                || provider.GetChildKeys([], segment).Any())
            {
                yield return segment;
            }
        }
    }

    /// <summary>
    /// Determines whether an earlier provider is overridden by the declarative route for a key. That
    /// happens when the route supplies the key at the first root both routes share (a chain reports a
    /// missing key as not found, so the earlier provider would otherwise win in the enclosing root).
    /// Such a root always exists because the candidate is never the declarative provider.
    /// </summary>
    /// <param name="candidate">The earlier provider.</param>
    /// <param name="declarative">The declarative provider.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns><see langword="true"/> if the declarative route supplies the key.</returns>
    private static bool IsKeyOverriddenByDeclarativeRoute(
        ProviderEntry candidate,
        ProviderEntry declarative,
        string key)
    {
        var declarativeRoute = declarative.SelfAndAncestors().FirstOrDefault(
            route => candidate.SelfAndAncestors().Any(route.HasSameOwnerOccurrence));

        return declarativeRoute != null && declarativeRoute.Provider.TryGet(key, out _);
    }

    /// <summary>
    /// Appends every leaf provider reachable from <paramref name="root"/> in ascending order of
    /// precedence. Chain providers are retained as parent nodes so analysis can honor their
    /// null-as-not-found boundary.
    /// </summary>
    /// <param name="root">The configuration root to flatten.</param>
    /// <param name="entries">The list that receives the leaf providers.</param>
    /// <param name="parent">The chain entry that contains <paramref name="root"/>, if any.</param>
    private static void Flatten(
        IConfigurationRoot root,
        List<ProviderEntry> entries,
        ProviderEntry? parent = null)
    {
        foreach (var provider in root.Providers)
        {
            var entry = new ProviderEntry(provider, root, parent);

            if (provider is ChainedConfigurationProvider { Configuration: IConfigurationRoot chained })
            {
                Flatten(chained, entries, entry);
            }
            else
            {
                entries.Add(entry);
            }
        }
    }

    internal sealed class Analysis
    {
        private Analysis(
            Failure failure,
            string? unavailableReason,
            string? filePath,
            IReadOnlyCollection<string> ignoredKeys,
            IReadOnlyList<UnmaskedProvider> unmaskedProviders)
        {
            this.Failure = failure;
            this.UnavailableReason = unavailableReason;
            this.FilePath = filePath;
            this.IgnoredKeys = ignoredKeys;
            this.UnmaskedProviders = unmaskedProviders;
        }

        public Failure Failure { get; }

        public string? UnavailableReason { get; }

        public string? FilePath { get; }

        public IReadOnlyCollection<string> IgnoredKeys { get; }

        public IReadOnlyList<UnmaskedProvider> UnmaskedProviders { get; }

        internal static Analysis Available(
            string filePath,
            IReadOnlyCollection<string> ignoredKeys,
            IReadOnlyList<UnmaskedProvider> unmaskedProviders) =>
            new(Failure.None, null, filePath, ignoredKeys, unmaskedProviders);

        internal static Analysis Unavailable(
            Failure failure,
            string reason) =>
            new(failure, reason, null, [], []);
    }

    internal sealed class UnmaskedProvider(
        IConfigurationProvider provider,
        IReadOnlyCollection<string> keys)
    {
        public bool IsEnvironmentVariables { get; } = provider is EnvironmentVariablesConfigurationProvider;

        public string ProviderType { get; } = provider.GetType().FullName ?? provider.GetType().Name;

        public IReadOnlyCollection<string> Keys { get; } = keys;
    }

    private sealed class ProviderEntry(
        IConfigurationProvider provider,
        IConfigurationRoot owner,
        ProviderEntry? parent)
    {
        public IConfigurationProvider Provider { get; } = provider;

        public IConfigurationRoot Owner { get; } = owner;

        public ProviderEntry? Parent { get; } = parent;

        public IEnumerable<ProviderEntry> SelfAndAncestors()
        {
            for (var entry = this; entry != null; entry = entry.Parent)
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this provider is reached through the same occurrence of the
        /// root that owns <paramref name="target"/>, directly or through chained roots.
        /// </summary>
        /// <param name="target">The provider whose owner occurrence is compared.</param>
        /// <returns><see langword="true"/> if this provider is within the target's owner occurrence.</returns>
        public bool IsWithinSameOwnerOccurrence(ProviderEntry target) =>
            this.SelfAndAncestors().Any(target.HasSameOwnerOccurrence);

        public bool HasSameOwnerOccurrence(ProviderEntry other) =>
            ReferenceEquals(this.Owner, other.Owner)
            && ReferenceEquals(this.Parent, other.Parent);

        /// <summary>
        /// Determines whether every chain between this provider and the application root exposes a
        /// non-empty value for <paramref name="key"/>. <see cref="ChainedConfigurationProvider"/>
        /// treats null and empty values as not found.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <returns><see langword="true"/> if the key is exposed through all containing chains.</returns>
        public bool IsExposedThroughContainingChains(string key) =>
            this.SelfAndAncestors().Skip(1).All(chain => chain.Provider.TryGet(key, out _));

        /// <summary>
        /// Determines whether this provider is directly in <paramref name="target"/>'s owner
        /// occurrence, or every intervening chain exposes a non-empty value for
        /// <paramref name="key"/> before reaching it.
        /// <para/>
        /// Callers only pass entries within the target's owner occurrence, so the walk up the chains
        /// always ends at it.
        /// </summary>
        /// <param name="target">The provider whose owner occurrence is the destination.</param>
        /// <param name="key">The configuration key.</param>
        /// <returns><see langword="true"/> if the provider's value for the key reaches the target.</returns>
        public bool IsExposedTo(ProviderEntry target, string key) => this.SelfAndAncestors()
            .TakeWhile(entry => !entry.HasSameOwnerOccurrence(target))
            .All(entry => entry.Parent?.Provider.TryGet(key, out _) == true);
    }
}
