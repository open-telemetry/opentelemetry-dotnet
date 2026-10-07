// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// </summary>
    /// <remarks>
    /// Meter names support wildcard matching:
    /// <list type="bullet">
    /// <item><description><c>*</c> matches zero or more characters;</description></item>
    /// <item><description><c>?</c> matches exactly one character.</description></item>
    /// </list>
    /// Escaping wildcard characters is not supported.
    /// </remarks>
    /// <param name="names">Meter names.</param>
    /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public abstract MeterProviderBuilder AddMeter(params string[] names);
}
