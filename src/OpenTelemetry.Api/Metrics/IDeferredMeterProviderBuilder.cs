// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes a meter provider builder that supports deferred initialization
/// using an <see cref="IServiceProvider"/> to perform dependency injection.
/// </summary>
public interface IDeferredMeterProviderBuilder
{
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="MeterProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    MeterProviderBuilder Configure(Action<IServiceProvider, MeterProviderBuilder> configure);
}