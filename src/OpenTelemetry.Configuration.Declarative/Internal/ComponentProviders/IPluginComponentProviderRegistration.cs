// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// A non-generic component provider registration, used to store registrations for different
/// component types in a single collection.
/// </summary>
internal interface IPluginComponentProviderRegistration
{
    /// <summary>
    /// Gets the type of component the registered provider creates.
    /// </summary>
    Type ComponentType { get; }

    /// <summary>
    /// Gets the name that selects this provider in a configuration document.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the registered concrete provider type.
    /// </summary>
    Type ProviderType { get; }

    /// <summary>
    /// Creates a component from the supplied configuration properties and services.
    /// </summary>
    /// <param name="properties">The configuration properties for the component.</param>
    /// <param name="serviceProvider">The services available to the provider.</param>
    /// <returns>The created component.</returns>
    object Create(ConfigProperties properties, IServiceProvider serviceProvider);
}
