// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// LoggerProviderBuilder base class.
/// </summary>
public abstract class LoggerProviderBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProviderBuilder"/> class.
    /// </summary>
    protected LoggerProviderBuilder()
    {
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    public abstract LoggerProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;
}
