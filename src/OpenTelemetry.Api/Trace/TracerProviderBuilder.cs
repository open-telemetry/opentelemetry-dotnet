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
    ///
    /// <remarks>
    /// When multiple matchers are added for the same source name, the <see cref="TracerProvider"/>
    /// will enable an ActivitySource if its name matches at least one of the provided names.
    /// </remarks>
    /// </summary>
    /// <param name="names">Activity source names.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public abstract TracerProviderBuilder AddSource(params string[] names);

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
