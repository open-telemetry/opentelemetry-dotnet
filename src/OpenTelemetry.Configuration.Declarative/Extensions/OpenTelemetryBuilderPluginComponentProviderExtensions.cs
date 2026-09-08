// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Extension methods for registering declarative configuration plugin component providers.
/// </summary>
internal static class OpenTelemetryBuilderPluginComponentProviderExtensions
{
    /// <summary>
    /// Registers <paramref name="provider"/> as the source of <typeparamref name="TComponent"/>
    /// components under <paramref name="name"/>.
    /// </summary>
    /// <typeparam name="TComponent">The type of component the provider creates.</typeparam>
    /// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> builder.</param>
    /// <param name="name">
    /// The case-sensitive name that selects the provider in a configuration document.
    /// </param>
    /// <param name="provider">The provider to register.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider instance for the same component type and name is already registered.
    /// </exception>
    public static IOpenTelemetryBuilder AddPluginComponentProvider<TComponent>(
        this IOpenTelemetryBuilder builder,
        string name,
        PluginComponentProvider<TComponent> provider)
        where TComponent : class
    {
        Guard.ThrowIfNull(builder);

        builder.Services.AddPluginComponentProvider(name, provider);
        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as the source of <typeparamref name="TComponent"/>
    /// components under <paramref name="name"/>, constructing it from the container so that it can
    /// take dependencies.
    /// </summary>
    /// <typeparam name="TComponent">The type of component the provider creates.</typeparam>
    /// <typeparam name="TProvider">The provider type to construct.</typeparam>
    /// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> builder.</param>
    /// <param name="name">
    /// The case-sensitive name that selects the provider in a configuration document.
    /// </param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider for the same component type and name is already registered.
    /// </exception>
#if NET
    public static IOpenTelemetryBuilder AddPluginComponentProvider<TComponent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
#else
    public static IOpenTelemetryBuilder AddPluginComponentProvider<TComponent, TProvider>(
#endif
        this IOpenTelemetryBuilder builder,
        string name)
        where TComponent : class
        where TProvider : PluginComponentProvider<TComponent>
    {
        Guard.ThrowIfNull(builder);

        builder.Services.AddPluginComponentProvider<TComponent, TProvider>(name);
        return builder;
    }
}
