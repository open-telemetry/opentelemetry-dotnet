// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// Represents a named registration for a provider that creates components of a specific type.
/// </summary>
/// <typeparam name="TComponent">The type of component created by the provider.</typeparam>
internal sealed class PluginComponentProviderRegistration<TComponent> : IPluginComponentProviderRegistration
    where TComponent : class
{
    private readonly Func<IServiceProvider, PluginComponentProvider<TComponent>> providerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginComponentProviderRegistration{TComponent}"/> class.
    /// </summary>
    /// <param name="name">The name that selects the provider in a configuration document.</param>
    /// <param name="providerType">The concrete provider type associated with the registration.</param>
    /// <param name="providerFactory">The function used to obtain the provider from the available services.</param>
    public PluginComponentProviderRegistration(
        string name,
        Type providerType,
        Func<IServiceProvider, PluginComponentProvider<TComponent>> providerFactory)
    {
        this.Name = name;
        this.ProviderType = providerType;
        this.providerFactory = providerFactory;
    }

    /// <inheritdoc />
    public Type ComponentType => typeof(TComponent);

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Type ProviderType { get; }

    /// <inheritdoc />
    public object Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => this.providerFactory(serviceProvider).Create(properties, serviceProvider);
}
