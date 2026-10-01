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
                "the application's IConfiguration is not an IConfigurationRoot");
        }

        var entries = new List<ProviderEntry>();
        Flatten(root, entries);

        var declarativeIndex = entries.FindLastIndex(entry => entry.Provider is DeclarativeConfigurationProvider);
        if (declarativeIndex < 0)
        {
            return Analysis.Unavailable(
                Failure.DeclarativeProviderNotFound,
                "no declarative configuration provider is reachable from the application's IConfiguration");
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
                    && IsBlockedByDeclarativeBranch(entry, declarativeEntry, key))
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

    private static bool IsImportedEnvironmentVariable(
        IConfigurationProvider provider,
        string key,
        DeclarativeConfigurationDocument document)
    {
        if (provider is not EnvironmentVariablesConfigurationProvider
            || !document.ReferencesEnvironmentVariable(key)
            || !provider.TryGet(key, out _))
        {
            return false;
        }

        // A prefixed environment provider can expose APP_OTEL_SERVICE_NAME as OTEL_SERVICE_NAME,
        // but substitution of ${OTEL_SERVICE_NAME} reads the unprefixed process variable. The
        // provider does not expose its prefix as an API; its documented string representation is
        // the only non-reflection metadata that distinguishes a prefixed provider.
        var providerDescription = provider.ToString();
        return providerDescription == null
            || providerDescription.IndexOf(" Prefix: '", StringComparison.Ordinal) < 0;
    }

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

    private static bool IsBlockedByDeclarativeBranch(
        ProviderEntry candidate,
        ProviderEntry declarative,
        string key)
    {
        // Returns true when the branch containing the selected declarative provider supplies the key at
        // the first root where the candidate and declarative provider routes diverge. In that case the
        // higher-precedence branch prevents an earlier provider in the enclosing root from being used.

        for (var declarativeRoute = declarative;
            declarativeRoute != null;
            declarativeRoute = declarativeRoute.Parent)
        {
            for (var candidateRoute = candidate;
                candidateRoute != null;
                candidateRoute = candidateRoute.Parent)
            {
                if (!declarativeRoute.HasSameOwnerOccurrence(candidateRoute))
                {
                    continue;
                }

                if (!ReferenceEquals(declarativeRoute, candidateRoute))
                {
                    return declarativeRoute.Provider.TryGet(key, out _);
                }

                break;
            }
        }

        return false;
    }

    private static void Flatten(
        IConfigurationRoot root,
        List<ProviderEntry> entries,
        ProviderEntry? parent = null)
    {
        // Appends every leaf provider reachable from root in ascending order of precedence. Chain
        // providers are retained as parent nodes so analysis can honor their null-as-not-found boundary.

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

        public bool IsWithinSameOwnerOccurrence(ProviderEntry target)
        {
            // True when this provider is reached through the same occurrence of the root that owns
            // target, directly or through chained roots.

            for (var entry = this; entry != null; entry = entry.Parent)
            {
                if (entry.HasSameOwnerOccurrence(target))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasSameOwnerOccurrence(ProviderEntry other) =>
            ReferenceEquals(this.Owner, other.Owner)
            && ReferenceEquals(this.Parent, other.Parent);

        public bool IsExposedThroughContainingChains(string key)
        {
            // True when every chain between this provider and the application root exposes a non-empty
            // value for key. ChainedConfigurationProvider treats null and empty values as not found.

            for (var entry = this.Parent; entry != null; entry = entry.Parent)
            {
                if (!entry.Provider.TryGet(key, out _))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsExposedTo(ProviderEntry target, string key)
        {
            // True when this provider is directly in target's owner occurrence, or every intervening
            // chain exposes a non-empty value for key before reaching it.

            if (this.HasSameOwnerOccurrence(target))
            {
                return true;
            }

            for (var entry = this.Parent; entry != null; entry = entry.Parent)
            {
                if (!entry.Provider.TryGet(key, out _))
                {
                    return false;
                }

                if (entry.HasSameOwnerOccurrence(target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
