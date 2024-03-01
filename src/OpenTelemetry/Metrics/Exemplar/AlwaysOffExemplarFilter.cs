// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// An <see cref="ExemplarFilter"/> implementation which makes no measurements
/// eligible for becoming an <see cref="Exemplar"/>.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alwaysoff"/>.
/// </remarks>
internal sealed class AlwaysOffExemplarFilter : ExemplarFilter
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
