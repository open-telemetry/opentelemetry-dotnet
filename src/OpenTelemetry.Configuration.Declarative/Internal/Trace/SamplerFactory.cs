// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the samplers named by a document, at any depth.
/// </summary>
internal static class SamplerFactory
{
    /// <summary>
    /// Creates the sampler described by <paramref name="component"/>.
    /// </summary>
    /// <param name="component">The sampler node.</param>
    /// <param name="serviceProvider">The services the tracer provider is built from.</param>
    /// <returns>The sampler. The caller owns it.</returns>
    /// <exception cref="DeclarativeConfigurationException">No provider can create the sampler, or the provider rejects its configuration.</exception>
    internal static Sampler Create(ComponentConfiguration component, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(component);
        Guard.ThrowIfNull(serviceProvider);

        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        if (!registry.Contains<Sampler>(component.Name))
        {
            throw BindingHelpers.CreateException(component.Path, component.Position, "is a sampler that is not supported by declarative configuration.");
        }

        return registry.Create<Sampler>(component.Name, component.Properties);
    }

    /// <summary>
    /// Creates the sampler held by the <paramref name="key"/> property of a composite sampler.
    /// </summary>
    /// <param name="parent">The properties of the composite sampler.</param>
    /// <param name="parentName">The name of the composite sampler, which starts the path in messages.</param>
    /// <param name="key">The property that holds the child sampler.</param>
    /// <param name="serviceProvider">The services the tracer provider is built from.</param>
    /// <returns>The sampler, or <see langword="null"/> when the property is absent.</returns>
    /// <exception cref="DeclarativeConfigurationException">The property is not a valid sampler.</exception>
    internal static Sampler? CreateChild(ConfigProperties parent, string parentName, string key, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(parent);

        if (!parent.TryGetValue(key, out var value))
        {
            return null;
        }

        var component = BindingHelpers.BindComponent(
            value,
            YamlPath.Child(parentName, key),
            "sampler",
            parent.GetKeyPosition(key));

        return Create(component, serviceProvider);
    }
}
