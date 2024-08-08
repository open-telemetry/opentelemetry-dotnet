// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// Propagators allow setting the global default Propagators.
/// </summary>
public static class Propagators
{
    private static readonly TextMapPropagator Noop = new NoopTextMapPropagator();

    /// <summary>
    /// Gets the Default TextMapPropagator to be used.
    /// </summary>
    /// <remarks>
    /// Setting this can be done only from Sdk.
    /// </remarks>
    public static TextMapPropagator DefaultTextMapPropagator { get; internal set; } = Noop;

    internal static void Reset()
    {
        DefaultTextMapPropagator = Noop;
    }
}
