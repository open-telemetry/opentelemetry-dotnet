// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Resolves registered component providers by component type and name, and creates components
/// from them.
/// </summary>
/// <remarks>
/// The index is built once during construction and never mutated afterwards, so
/// <see cref="Create{TComponent}(string, ConfigProperties)"/> is safe to call concurrently and
/// re-entrantly.
/// </remarks>
internal sealed class PluginComponentProviderRegistry
{
#if NET
    private readonly FrozenDictionary<ComponentProviderKey, IPluginComponentProviderRegistration> registrations;
#else
    private readonly Dictionary<ComponentProviderKey, IPluginComponentProviderRegistration> registrations;
#endif
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginComponentProviderRegistry"/> class over
    /// the providers registered in <paramref name="serviceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">
    /// The services the providers were registered into. Taking the providers from the same
    /// container that is handed to them guarantees a provider sees its own registrations.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider has no usable name, or when two providers share a component type and name.
    /// </exception>
    public PluginComponentProviderRegistry(IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(serviceProvider);

        this.serviceProvider = serviceProvider;
        this.registrations = BuildIndex(serviceProvider.GetServices<IPluginComponentProviderRegistration>());
    }

    /// <summary>
    /// Creates the <typeparamref name="TComponent"/> registered under <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Nothing is cached: each call creates a new component, so a component type appearing at two
    /// configuration nodes yields two instances. Exceptions thrown by the provider propagate
    /// unchanged.
    /// </remarks>
    /// <typeparam name="TComponent">The type of component to create. Matched exactly; a derived type does not resolve a provider registered for its base.</typeparam>
    /// <param name="name">The name that selects the provider.</param>
    /// <param name="properties">The configuration node for the component.</param>
    /// <returns>The created component.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="properties"/> is <see langword="null"/>.</exception>
    /// <exception cref="DeclarativeConfigurationException">Thrown when no provider is registered for the component type and name.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registered provider returns <see langword="null"/> or a component of an incompatible type.
    /// </exception>
    public TComponent Create<TComponent>(string name, ConfigProperties properties)
        where TComponent : class
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(properties);

        if (!this.registrations.TryGetValue(new(typeof(TComponent), name), out var registration))
        {
            var registered = this.DescribeRegisteredNames(typeof(TComponent));

            OpenTelemetryDeclarativeConfigurationEventSource.Log.ComponentProviderNotFound(
                typeof(TComponent).ToString(),
                name,
                registered);

            throw new DeclarativeConfigurationException(
                $"No component provider is registered for component type '{typeof(TComponent)}' with name '{name}'. {registered}");
        }

        var created = registration.Create(properties, this.serviceProvider);

        if (created is not TComponent component)
        {
            var actualType = created is null ? "null" : $"component type '{created.GetType()}'";

            throw new InvalidOperationException(
                $"Component provider '{registration.ProviderType}' registered for component type '{typeof(TComponent)}' with name '{name}' returned {actualType}.");
        }

        OpenTelemetryDeclarativeConfigurationEventSource.Log.ComponentCreated(typeof(TComponent).ToString(), name);

        return component;
    }

#if NET
    private static FrozenDictionary<ComponentProviderKey, IPluginComponentProviderRegistration> BuildIndex(
#else
    private static Dictionary<ComponentProviderKey, IPluginComponentProviderRegistration> BuildIndex(
#endif
        IEnumerable<IPluginComponentProviderRegistration> registrations)
    {
        var index = new Dictionary<ComponentProviderKey, IPluginComponentProviderRegistration>();

        foreach (var registration in registrations)
        {
            if (string.IsNullOrWhiteSpace(registration.Name))
            {
                throw new InvalidOperationException(
                    $"Component provider registration '{registration.GetType()}' has no name. Name must return a non-empty value.");
            }

            var key = new ComponentProviderKey(registration.ComponentType, registration.Name);

            if (index.TryGetValue(key, out var existing))
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.DuplicateComponentProviderRejected(
                    key.ComponentType.ToString(),
                    key.Name,
                    existing.ProviderType.ToString(),
                    registration.ProviderType.ToString());

                throw new InvalidOperationException(
                    $"Two component providers are registered for component type '{key.ComponentType}' with name '{key.Name}': '{existing.ProviderType}' and '{registration.ProviderType}'. Each component type and name combination must be unique.");
            }

            index.Add(key, registration);
        }

#if NET
        return index.ToFrozenDictionary();
#else
        return index;
#endif
    }

    private string DescribeRegisteredNames(Type componentType)
    {
        var names = this.registrations.Keys
            .Where(key => key.ComponentType == componentType)
            .Select(key => $"'{key.Name}'")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return names.Count == 0
            ? "No component providers are registered for this component type."
            : $"Registered names for this component type: {string.Join(", ", names)}.";
    }

    private readonly record struct ComponentProviderKey(Type ComponentType, string Name);
}
