// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// LoggerProviderBuilder base class.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
public
#else
/// <summary>
/// LoggerProviderBuilder base class.
/// </summary>
internal
#endif
    abstract class LoggerProviderBuilder
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