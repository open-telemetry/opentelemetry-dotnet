// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// This file calls AddOpenTelemetryDeclarativeConfiguration, which carries the
// same experimental attribute as the public API. Suppress once here rather than
// at every call site.
#pragma warning disable OTEL1006

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Extension methods for wiring declarative configuration through <see cref="IOpenTelemetryBuilder"/>.
/// </summary>
public static class OpenTelemetryBuilderDeclarativeConfigurationExtensions
{
    /// <summary>
    /// Adds the declarative configuration (YAML) source into DI, reading the path from <c>OTEL_CONFIG_FILE</c>.
    /// </summary>
    /// <remarks>
    /// Appends YAML after existing sources (YAML overrides earlier env/appsettings; sources added
    /// later override YAML). Inserts in-place on <see cref="ConfigurationManager"/> when
    /// possible; otherwise wraps the existing root. No-op when <c>OTEL_CONFIG_FILE</c> is unset,
    /// empty, or whitespace. See
    /// <see href="https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/diagnostics/experimental-apis/OTEL1006.md">OTEL1006</see>
    /// for call-order pitfalls.
    /// </remarks>
    /// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> builder.</param>
    /// <returns>The original <see cref="IOpenTelemetryBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    [Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
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
    [Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public static IOpenTelemetryBuilder UseDeclarativeConfiguration(
        this IOpenTelemetryBuilder builder,
        string filePath)
    {
        Guard.ThrowIfNull(builder);

        AddDeclarativeConfigurationOverlay(builder.Services, new FilePath(filePath));
        return builder;
    }

    internal static void AddDeclarativeConfigurationOverlay(IServiceCollection services, FilePath filePath)
    {
        // Second call on the same IServiceCollection is a no-op (first file path wins).
        var existingMarker = services
            .Select(d => d.ImplementationInstance)
            .OfType<DeclarativeConfigurationOverlayMarker>()
            .FirstOrDefault();

        if (existingMarker != null)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.DeclarativeConfigurationAlreadyRegistered(existingMarker.FilePath.ToString(), filePath.ToString());
            return;
        }

        services.AddSingleton(new DeclarativeConfigurationOverlayMarker(filePath));
        OpenTelemetryDeclarativeConfigurationEventSource.Log.OverlayRegistrationStarted(filePath.ToString());

        // TODO(strict-mode): branch here on a future DeclarativeConfigurationMode (Default vs Strict). See #6380.

        // Last registered IConfiguration wins in DI.
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IConfiguration));

        if (descriptor?.ImplementationInstance is IConfigurationBuilder liveBuilder)
        {
            // ConfigurationManager registered as instance: mutate in-place, preserve reload.
            liveBuilder.AddOpenTelemetryDeclarativeConfiguration(filePath);
            return;
        }

        // Factory/type registration: replace descriptor, wrap or insert on first resolve.
        var existingFactory = descriptor?.ImplementationFactory;
        var existingInstance = descriptor?.ImplementationInstance as IConfiguration;
        var existingType = descriptor?.ImplementationType;
        var lifetime = descriptor?.Lifetime ?? ServiceLifetime.Singleton;

        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
        else
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.NoExistingConfigurationRegistered(filePath.ToString());
        }

        services.Add(ServiceDescriptor.Describe(
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
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.PriorConfigurationResolutionFailed(filePath.ToString());
                }

                if (existing is IConfigurationBuilder existingAsBuilder)
                {
                    // Resolved config is a live builder (HostApplicationBuilder): insert in-place.
                    existingAsBuilder.AddOpenTelemetryDeclarativeConfiguration(filePath);
                    return existing;
                }

                // ConfigurationRoot: chain existing, append YAML last. alreadyRegistered is deferred to
                // resolve time because HostBuilder's factory-built root is not available until then.
                var alreadyRegistered = existing is IConfigurationRoot existingRoot &&
                    existingRoot.Providers.OfType<DeclarativeConfigurationProvider>().Any(p =>
                        p.FilePath == filePath);

#pragma warning disable CA2000 // Ownership transferred to DI container via factory return value; lifetime matches the replaced descriptor's lifetime
                var manager = new ConfigurationManager();
#pragma warning restore CA2000

                if (existing != null)
                {
                    manager.AddConfiguration(existing);
                }

                if (alreadyRegistered)
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.SourceAlreadyPresentInExistingConfiguration(filePath.ToString());
                }
                else
                {
                    manager.AddOpenTelemetryDeclarativeConfiguration(filePath);
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
