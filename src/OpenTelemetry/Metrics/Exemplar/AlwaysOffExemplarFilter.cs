// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// An ExemplarFilter which makes no measurements eligible for being an Exemplar.
/// Using this ExemplarFilter is as good as disabling Exemplar feature.
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
public
#else
/// <summary>
/// An ExemplarFilter which makes no measurements eligible for being an Exemplar.
/// Using this ExemplarFilter is as good as disabling Exemplar feature.
/// </summary>
internal
#endif
    sealed class AlwaysOffExemplarFilter : ExemplarFilter
{
    /// <inheritdoc/>
    public override bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return false;
    }

    /// <inheritdoc/>
    public override bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return false;
    }
}