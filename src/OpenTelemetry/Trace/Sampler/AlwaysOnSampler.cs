// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampler implementation which always returns <c>SamplingDecision.RecordAndSample</c>.
/// </summary>
public sealed class AlwaysOnSampler : Sampler
{
    /// <inheritdoc />
    public override SamplingResult ShouldSample(in SamplingParameters _)
    {
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
