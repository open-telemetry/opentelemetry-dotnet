// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Metrics;

/// <summary>
/// MeterProviderBuilder base class.
/// </summary>
public abstract class MeterProviderBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    protected MeterProviderBuilder()
    {
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public abstract MeterProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;

    /// <summary>
    /// Adds given Meter names to the list of subscribed meters.
    /// <remarks>
    /// When multiple matchers are added for the same meter name, the <see cref="MeterProvider"/>
    /// will enable a meter if its name matches at least one of the provided names.
    /// </remarks>
    /// </summary>
    /// <param name="names">Meter names.</param>
    /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public abstract MeterProviderBuilder AddMeter(params string[] names);
}
