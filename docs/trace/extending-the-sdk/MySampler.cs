// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

internal sealed class MySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        Console.WriteLine($"MySampler.ShouldSample({samplingParameters.Name})");
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
