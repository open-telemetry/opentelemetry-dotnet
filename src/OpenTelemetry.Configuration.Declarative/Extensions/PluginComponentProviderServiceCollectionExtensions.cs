// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Configuration;
using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering component providers, which create the components a
/// declarative configuration document names.
/// </summary>
internal static class PluginComponentProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <paramref name="provider"/> as the source of <typeparamref name="TComponent"/>
    /// components under <paramref name="name"/>.
    /// </summary>
    /// <typeparam name="TComponent">The type of component the provider creates.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="name">
    /// The case-sensitive name that selects the provider in a configuration document.
    /// </param>
    /// <param name="provider">The provider to register.</param>
    /// <returns>The supplied <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider instance for the same component type and name is already registered.
    /// </exception>
    public static IServiceCollection AddPluginComponentProvider<TComponent>(
        this IServiceCollection services,
        string name,
        PluginComponentProvider<TComponent> provider)
        where TComponent : class
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNullOrWhitespace(name);
        Guard.ThrowIfNull(provider);

        return AddPluginComponentProvider(
            services,
            new PluginComponentProviderRegistration<TComponent>(
                name,
                provider.GetType(),
                _ => provider));
    }

    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as the source of <typeparamref name="TComponent"/>
    /// components under <paramref name="name"/>, constructing it from the container so that it can
    /// take dependencies.
    /// </summary>
    /// <typeparam name="TComponent">The type of component the provider creates.</typeparam>
    /// <typeparam name="TProvider">The provider type to construct.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="name">
    /// The case-sensitive name that selects the provider in a configuration document.
    /// </param>
    /// <returns>The supplied <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider for the same component type and name is already registered.
    /// </exception>
#if NET
    public static IServiceCollection AddPluginComponentProvider<TComponent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
#else
    public static IServiceCollection AddPluginComponentProvider<TComponent, TProvider>(
#endif
        this IServiceCollection services,
        string name)
        where TComponent : class
        where TProvider : PluginComponentProvider<TComponent>
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNullOrWhitespace(name);

        return AddPluginComponentProvider(
            services,
            new PluginComponentProviderRegistration<TComponent>(
                name,
                typeof(TProvider),
                static sp => sp.GetRequiredService<TProvider>()),
            static services => services.TryAddSingleton<TProvider>());
    }

    private static IServiceCollection AddPluginComponentProvider(
        IServiceCollection services,
        IPluginComponentProviderRegistration registration,
        Action<IServiceCollection>? registerProvider = null)
    {
        ThrowIfDuplicate(
            services,
            registration.ComponentType,
            registration.Name,
            registration.ProviderType);

        registerProvider?.Invoke(services);

        services.Add(ServiceDescriptor.Singleton(registration));
        services.TryAddSingleton<PluginComponentProviderRegistry>();

        OpenTelemetryDeclarativeConfigurationEventSource.Log.ComponentProviderRegistered(
            registration.ComponentType.ToString(),
            registration.Name,
            registration.ProviderType.ToString());

        return services;
    }

    private static void ThrowIfDuplicate(
        IServiceCollection services,
        Type componentType,
        string name,
        Type providerType)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IPluginComponentProviderRegistration)
                || descriptor.ImplementationInstance is not IPluginComponentProviderRegistration existing
                || existing.ComponentType != componentType
                || !string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            OpenTelemetryDeclarativeConfigurationEventSource.Log.DuplicateComponentProviderRejected(
                componentType.ToString(),
                name,
                existing.ProviderType.ToString(),
                providerType.ToString());

            throw new InvalidOperationException(
                $"A component provider for component type '{componentType}' with name '{name}' is already registered by '{existing.ProviderType}', so '{providerType}' cannot be registered. Each component type and name combination must be unique.");
        }
    }
}
