// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Extension methods for wiring declarative configuration through <see cref="IOpenTelemetryBuilder"/>.
/// </summary>
public static class OpenTelemetryBuilderDeclarativeConfigurationExtensions
{
    /// <summary>
    /// Adds the declarative configuration (YAML) source into DI, reading the path from the <c>OTEL_CONFIG_FILE</c> environment variable.
    /// </summary>
    /// <remarks>
    /// Appends YAML after existing sources (YAML overrides earlier env/appsettings; sources added
    /// later override YAML). Inserts in-place on <see cref="ConfigurationManager"/> when
    /// possible; otherwise wraps the existing root. No-op when <c>OTEL_CONFIG_FILE</c> is unset,
    /// empty, or whitespace.
    /// </remarks>
    /// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> builder.</param>
    /// <returns>The original <see cref="IOpenTelemetryBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IOpenTelemetryBuilder UseDeclarativeConfiguration(
        this IOpenTelemetryBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        var filePath = Environment.GetEnvironmentVariable(OtelEnvironmentVariables.ConfigFile);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.OtelConfigFileNotSet();
            return builder;
        }

        return builder.UseDeclarativeConfiguration(filePath);
    }

    /// <summary>
    /// Adds the declarative configuration (YAML) source into DI using the supplied file path.
    /// </summary>
    /// <remarks><inheritdoc cref="UseDeclarativeConfiguration(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> builder.</param>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>The original <see cref="IOpenTelemetryBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null, empty, or whitespace.</exception>
    public static IOpenTelemetryBuilder UseDeclarativeConfiguration(
        this IOpenTelemetryBuilder builder,
        string filePath)
    {
        Guard.ThrowIfNull(builder);

        AddDeclarativeConfigurationOverlay(builder.Services, new FilePath(filePath));
        return builder;
    }

    /// <summary>
    /// Adds declarative configuration to services configured by an OpenTelemetry builder.
    /// </summary>
    /// <param name="services">The services used to configure OpenTelemetry.</param>
    /// <param name="filePath">The path to the declarative configuration file.</param>
    internal static void AddDeclarativeConfigurationOverlay(IServiceCollection services, FilePath filePath)
    {
        // Second call on the same IServiceCollection is a no-op (first file path wins).
        var existingMarker = services
            .Select(d => d.ImplementationInstance)
            .OfType<DeclarativeConfigurationOverlayMarker>()
            .FirstOrDefault();

        if (existingMarker != null)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.DeclarativeConfigurationAlreadyRegistered(existingMarker.FilePath.DisplayPath, filePath.DisplayPath);
            return;
        }

        // A host registers IConfiguration as a factory, so the application's live configuration
        // cannot be reached through the descriptor below. AddOpenTelemetry(IHostApplicationBuilder)
        // contributes an accessor that makes the instance reachable here.
        var configurationAccessor = services
            .LastOrDefault(d => d.ServiceType == typeof(OpenTelemetryBuilderConfigurationAccessor))
            ?.ImplementationInstance as OpenTelemetryBuilderConfigurationAccessor;

        // The accessor this call contributes if no declarative source is registered already. When one
        // is, the existing accessor wins and this instance is discarded unused.
        var candidateAccessor = new DeclarativeConfigurationDocumentAccessor(filePath);

        services.AddSingleton(new DeclarativeConfigurationOverlayMarker(filePath));

        OpenTelemetryDeclarativeConfigurationEventSource.Log.OverlayRegistrationStarted(filePath.DisplayPath);

        // TODO(strict-mode): branch here on a future DeclarativeConfigurationMode (Default vs Strict).
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380.

        // Fast path: hosting API accessor exposes a live ConfigurationManager; mutate in-place and skip descriptor scan.
        if (configurationAccessor?.Configuration is IConfigurationBuilder accessorBuilder)
        {
            accessorBuilder.AddOpenTelemetryDeclarativeConfiguration(candidateAccessor);
            services.TryAddSingleton(
                DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(accessorBuilder)
                    ?? candidateAccessor);
            return;
        }

        // Last registered IConfiguration wins in DI.
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IConfiguration));

        // IConfiguration is a singleton instance that is also a live builder; mutate in-place.
        if (descriptor?.ImplementationInstance is IConfigurationBuilder instanceBuilder)
        {
            instanceBuilder.AddOpenTelemetryDeclarativeConfiguration(candidateAccessor);
            services.TryAddSingleton(
                DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(instanceBuilder)
                    ?? candidateAccessor);
            return;
        }

        // Explicit singleton registration. Resolving IConfiguration first is what makes the scan
        // meaningful: the source below is only inserted while the configuration is being built, so
        // until that has happened there is nothing to find.
        services.TryAddSingleton(sp =>
            DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(sp)
                ?? candidateAccessor);

        // Factory/type registration: replace descriptor, wrap or insert on first resolve.
        var existingFactory = descriptor?.ImplementationFactory;
        var existingInstance = descriptor?.ImplementationInstance as IConfiguration;
        var existingType = descriptor?.ImplementationType;
        var lifetime = descriptor?.Lifetime ?? ServiceLifetime.Singleton;

        if (descriptor == null)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.NoExistingConfigurationRegistered(filePath.DisplayPath);
        }

        // Replace() targets the first IConfiguration descriptor, not the last one captured above.
        // Equivalent to Remove+Add for single registrations; harmless for multiple (last-wins).
        services.Replace(ServiceDescriptor.Describe(
            typeof(IConfiguration),
            sp =>
            {
                IConfiguration? existing = existingInstance
                    ?? (IConfiguration?)existingFactory?.Invoke(sp)
                    ?? (existingType != null
                        ? ActivatorUtilities.GetServiceOrCreateInstance(sp, existingType) as IConfiguration
                        : null);

                if (existing == null && descriptor != null)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.PriorConfigurationResolutionFailed(filePath.DisplayPath);
                }

                if (existing is IConfigurationBuilder existingAsBuilder)
                {
                    // Resolved config is a live builder (HostApplicationBuilder): insert in-place.
                    existingAsBuilder.AddOpenTelemetryDeclarativeConfiguration(candidateAccessor);
                    return existing;
                }

                // ConfigurationRoot: chain existing, append YAML last. alreadyRegistered is deferred to
                // resolve time because HostBuilder's factory-built root is not available until then.
                var existingAccessor = DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(existing);
                var alreadyRegistered = existingAccessor != null;

#pragma warning disable CA2000 // Ownership transferred to DI container via factory return value; lifetime matches the replaced descriptor's lifetime
                var manager = new ConfigurationManager();
#pragma warning restore CA2000

                if (existing != null)
                {
                    manager.AddConfiguration(existing);
                }

                if (alreadyRegistered)
                {
                    if (existingAccessor!.FilePath == filePath)
                    {
                        OpenTelemetryDeclarativeConfigurationEventSource.Log.SourceAlreadyPresentInExistingConfiguration(
                            filePath.DisplayPath);
                    }
                    else
                    {
                        OpenTelemetryDeclarativeConfigurationEventSource.Log.DifferentSourceAlreadyRegistered(
                            existingAccessor.FilePath.DisplayPath,
                            filePath.DisplayPath);
                    }
                }
                else
                {
                    manager.AddOpenTelemetryDeclarativeConfiguration(candidateAccessor);
                }

                return manager;
            },
            lifetime));
    }

    private sealed class DeclarativeConfigurationOverlayMarker(FilePath filePath)
    {
        internal FilePath FilePath { get; } = filePath;
    }
}
