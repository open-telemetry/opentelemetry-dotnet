// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace LinksAndParentBasedSamplerExample;

/// <summary>
///  An example of a composite sampler that has:
///  1. A parent based sampler.
///  2. A links based sampler.
///  The composite sampler first delegates to the parent based sampler and then to the
///  links based sampler. If either of these samplers decide to sample,
///  this composite sampler decides to sample.
/// </summary>
internal sealed class LinksAndParentBasedSampler : Sampler
{
    private readonly ParentBasedSampler parentBasedSampler;
    private readonly LinksBasedSampler linksBasedSampler;

    public LinksAndParentBasedSampler(ParentBasedSampler parentBasedSampler)
    {
        this.parentBasedSampler = parentBasedSampler ?? throw new ArgumentNullException(nameof(parentBasedSampler));
        this.linksBasedSampler = new LinksBasedSampler();
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var samplingResult = this.parentBasedSampler.ShouldSample(samplingParameters);
        if (samplingResult.Decision != SamplingDecision.Drop)
        {
            Console.WriteLine($"{samplingParameters.TraceId}: ParentBasedSampler decision: RecordAndSample");
            return samplingResult;
        }

        Console.WriteLine($"{samplingParameters.TraceId}: ParentBasedSampler decision: Drop");

        return this.linksBasedSampler.ShouldSample(samplingParameters);
    }
}
