// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests;

internal class TestSampler : Sampler
{
    public Func<SamplingParameters, SamplingResult> SamplingAction { get; set; }

    public SamplingParameters LatestSamplingParameters { get; private set; }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        this.LatestSamplingParameters = samplingParameters;
        return this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
