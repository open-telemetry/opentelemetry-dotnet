// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes a <see cref="MeterProviderBuilder"/> backed by an <see cref="IServiceCollection"/>.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IMeterProviderBuilder : IDeferredMeterProviderBuilder
{
    /// <summary>
    /// Gets the <see cref="MeterProvider"/> being constructed by the builder.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="Provider"/> should return <see langword="null"/> until
    /// construction has started and the <see cref="IServiceCollection"/> has
    /// closed.
    /// </remarks>
    MeterProvider? Provider { get; }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where metric services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Metric services are only available during the application
    /// configuration phase. This method should throw a <see
    /// cref="NotSupportedException"/> if services are configured after the
    /// application <see cref="IServiceProvider"/> has been created.
    /// </remarks>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure);
}