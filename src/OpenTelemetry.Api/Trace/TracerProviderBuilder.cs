// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// TracerProviderBuilder base class.
/// </summary>
public abstract class TracerProviderBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracerProviderBuilder"/> class.
    /// </summary>
    protected TracerProviderBuilder()
    {
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;

    /// <summary>
    /// Adds the given <see cref="ActivitySource"/> names to the list of subscribed sources.
    /// </summary>
    /// <param name="names">Activity source names.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddSource(params string[] names);

    /// <summary>
    /// Adds filter for <see cref="ActivitySource"/> to subscribe to.
    /// </summary>
    /// <param name="sourceFilter">Activity Source filter - if it returns true, OpenTelemetry subscribes to the corresponding source.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddSource(Func<ActivitySource, bool> sourceFilter);

    /// <summary>
    /// Adds a listener for <see cref="Activity"/> objects created with the given operation name to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// This is provided to capture legacy <see cref="Activity"/> objects created without using the <see cref="ActivitySource"/> API.
    /// </remarks>
    /// <param name="operationName">Operation name of the <see cref="Activity"/> objects to capture.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddLegacySource(string operationName);
}
