// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampler implementation which always returns <c>SamplingDecision.Drop</c>.
/// </summary>
public sealed class AlwaysOffSampler : Sampler
{
    /// <inheritdoc />
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        return new SamplingResult(SamplingDecision.Drop);
    }
}
