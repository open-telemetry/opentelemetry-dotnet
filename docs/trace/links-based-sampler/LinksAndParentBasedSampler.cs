// <copyright file="LinksAndParentBasedSampler.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
internal class LinksAndParentBasedSampler : Sampler
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
