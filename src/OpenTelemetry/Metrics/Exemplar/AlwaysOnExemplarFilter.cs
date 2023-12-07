// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// An ExemplarFilter which makes all measurements eligible for being an Exemplar.
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
public
#else
/// <summary>
/// An ExemplarFilter which makes all measurements eligible for being an Exemplar.
/// </summary>
internal
#endif
    sealed class AlwaysOnExemplarFilter : ExemplarFilter
{
    /// <inheritdoc/>
    public override bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return true;
    }

    /// <inheritdoc/>
    public override bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return true;
    }
}