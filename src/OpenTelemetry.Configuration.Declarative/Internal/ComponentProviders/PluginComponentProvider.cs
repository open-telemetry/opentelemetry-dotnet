// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// The base class for a provider that creates <typeparamref name="TComponent"/> instances from a
/// configuration node.
/// </summary>
/// <remarks>
/// A provider is a stateless factory and must be thread-safe: the same instance can be asked for a
/// component concurrently. Constructor dependencies must be safe to use from a singleton.
/// </remarks>
/// <typeparam name="TComponent">The type of component created.</typeparam>
internal abstract class PluginComponentProvider<TComponent>
    where TComponent : class
{
    /// <summary>
    /// Creates a component from the supplied configuration properties and services.
    /// </summary>
    /// <remarks>
    /// Called for every component the configuration names; the registry does not cache results,
    /// and providers should not cache the value they return. Throw a
    /// <see cref="Declarative.DeclarativeConfigurationException"/> when the supplied properties do
    /// not satisfy the component's configuration schema.
    /// </remarks>
    /// <param name="properties">The configuration properties for the component.</param>
    /// <param name="serviceProvider">The services available to the provider.</param>
    /// <returns>The created component.</returns>
    public abstract TComponent Create(ConfigProperties properties, IServiceProvider serviceProvider);
}
