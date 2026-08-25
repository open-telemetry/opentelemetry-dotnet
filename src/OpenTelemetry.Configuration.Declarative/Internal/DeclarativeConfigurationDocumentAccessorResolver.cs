// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Locates the <see cref="DeclarativeConfigurationDocumentAccessor"/> for an application, whether it
/// was registered explicitly by <c>UseDeclarativeConfiguration</c> or reached the application only as
/// an <see cref="IConfigurationSource"/> added through <c>IConfigurationBuilder</c>.
/// </summary>
internal static class DeclarativeConfigurationDocumentAccessorResolver
{
    // This type exists for situations when IConfigurationSource is added through IConfigurationBuilder.
    // IConfigurationBuilder</c> carries no IServiceCollection, so a source added that way cannot register
    // anything into DI. Scanning the resolved IConfiguration recovers the accessor the configuration
    // provider is already using, so a typed consumer reads the same document rather than parsing the
    // file a second time.

    /// <summary>
    /// Finds the accessor for <paramref name="serviceProvider"/>, preferring an explicit
    /// registration and falling back to a scan of the registered <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> to resolve from.</param>
    /// <returns>
    /// The accessor, or <see langword="null"/> when the application has no declarative
    /// configuration.
    /// </returns>
    internal static DeclarativeConfigurationDocumentAccessor? Find(IServiceProvider serviceProvider)
    {
        var accessor = serviceProvider.GetService<DeclarativeConfigurationDocumentAccessor>()
            ?? FindInConfiguration(serviceProvider);

        if (accessor == null)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.DocumentAccessorNotAvailable();
        }

        return accessor;
    }

    /// <summary>
    /// Resolves <see cref="IConfiguration"/> from <paramref name="serviceProvider"/> and scans it for
    /// an accessor, without consulting an explicit registration.
    /// </summary>
    /// <remarks>
    /// Resolving <see cref="IConfiguration"/> is what makes the result stable: the declarative source
    /// may only be inserted while the configuration is being built, so the scan has to run after
    /// that has happened. It does not matter whether <c>Load()</c> has run, because the accessor
    /// parses on demand.
    /// </remarks>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> to resolve from.</param>
    /// <returns>The accessor, or <see langword="null"/> when none is present.</returns>
    internal static DeclarativeConfigurationDocumentAccessor? FindInConfiguration(IServiceProvider serviceProvider) =>
        FindInConfiguration(serviceProvider.GetService<IConfiguration>());

    /// <summary>
    /// Scans a configuration builder's pending sources for a declarative accessor.
    /// </summary>
    /// <param name="builder">The builder whose sources are searched.</param>
    /// <returns>The accessor, or <see langword="null"/> when none is present.</returns>
    internal static DeclarativeConfigurationDocumentAccessor? FindInConfiguration(IConfigurationBuilder builder) =>
        FindInConfigurationCore(builder);

    /// <summary>
    /// Scans a configuration for a declarative accessor.
    /// </summary>
    /// <param name="configuration">
    /// An <see cref="IConfigurationRoot"/> whose providers are searched, or a
    /// <see cref="ConfigurationManager"/> whose sources are searched in preference to its providers
    /// because a source added to it is visible before its provider is built.
    /// </param>
    /// <remarks>
    /// Registering a declarative source twice on one builder is refused, so a single builder cannot
    /// hold two documents. Two can still become reachable when one arrives through
    /// <c>AddConfiguration</c>, because a chained configuration is opaque to that guard. Flat values
    /// then follow standard <see cref="IConfiguration"/> ordering, where the last provider to supply
    /// a key wins, so the last document reachable here is the one whose values an application
    /// observes and therefore the one returned. The others are reported and ignored: the typed
    /// document cannot be merged the way flat keys can, so one has to be chosen.
    /// </remarks>
    /// <returns>The accessor, or <see langword="null"/> when none is present.</returns>
    internal static DeclarativeConfigurationDocumentAccessor? FindInConfiguration(IConfiguration? configuration) =>
        FindInConfigurationCore(configuration);

    private static DeclarativeConfigurationDocumentAccessor? FindInConfigurationCore(object? configuration)
    {
        var accessors = new List<DeclarativeConfigurationDocumentAccessor>(capacity: 1);

        Collect(configuration, accessors);

        if (accessors.Count == 0)
        {
            return null;
        }

        var selected = accessors[accessors.Count - 1];

        for (var i = 0; i < accessors.Count - 1; i++)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.MultipleConfigurationDocumentsReachable(
                selected.FilePath.DisplayPath,
                accessors[i].FilePath.DisplayPath);
        }

        return selected;
    }

    /// <summary>
    /// Appends every accessor reachable from <paramref name="configuration"/> to
    /// <paramref name="accessors"/>, in ascending order of configuration precedence.
    /// </summary>
    private static void Collect(
        object? configuration,
        List<DeclarativeConfigurationDocumentAccessor> accessors)
    {
        if (configuration is IConfigurationBuilder builder)
        {
            // A builder's source list is the complete and authoritative view: every provider is
            // built from a source, and a source added to a ConfigurationManager is visible before
            // its provider exists. Walking its providers as well would count each document twice.
            foreach (var source in builder.Sources)
            {
                switch (source)
                {
                    case DeclarativeConfigurationSource declarative:
                        Add(declarative.Accessor, accessors);
                        break;

                    case ChainedConfigurationSource chained:
                        Collect(chained.Configuration, accessors);
                        break;
                }
            }

            return;
        }

        if (configuration is not IConfigurationRoot root)
        {
            return;
        }

        foreach (var provider in root.Providers)
        {
            switch (provider)
            {
                case DeclarativeConfigurationProvider declarative:
                    Add(declarative.Accessor, accessors);
                    break;

                // A configuration chained in via AddConfiguration is reachable only through the
                // chaining provider, so recurse. UseDeclarativeConfiguration builds exactly this
                // shape when it wraps an existing root, and so does any application that composes
                // its configuration from another IConfigurationRoot.
                case ChainedConfigurationProvider chained:
                    Collect(chained.Configuration, accessors);
                    break;
            }
        }
    }

    private static void Add(
        DeclarativeConfigurationDocumentAccessor accessor,
        List<DeclarativeConfigurationDocumentAccessor> accessors)
    {
        // One accessor can be reachable by more than one route, for example a root chained in twice.
        // That is one document rather than an ambiguous configuration, so it is recorded once, at
        // the highest precedence position it occupies.
        accessors.Remove(accessor);
        accessors.Add(accessor);
    }
}
