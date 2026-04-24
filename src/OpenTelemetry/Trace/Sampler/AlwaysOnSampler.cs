// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampler implementation which always returns <c>SamplingDecision.RecordAndSample</c>.
/// </summary>
public sealed class AlwaysOnSampler : Sampler
{
    internal static readonly AlwaysOnSampler Instance = new();

    /// <inheritdoc />
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) => new(SamplingDecision.RecordAndSample);
}
