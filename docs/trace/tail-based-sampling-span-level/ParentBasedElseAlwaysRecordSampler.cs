// <copyright file="ParentBasedElseAlwaysRecordSampler.cs" company="OpenTelemetry Authors">
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

namespace SDKBasedSpanLevelTailSamplingSample;

/// <summary>
///  Note: This is a proof-of-concept and is not meant to be used directly in production.
///  This is a composite sampler used to achieve a combination of parent-based sampling
///  and SDK-side "span-level" tail-based sampling.
///  It first invokes a head-sampling mechanism using the parent based sampling approach.
///  If the parent based sampler's decision is to sample it (i.e., record and export the span),
///  it retains that decision. If not, it returns a "record-only" sampling result that can be
///  changed later by a span processor based on span attributes (e.g., failure) that become
///  available only by the end of the span.
/// </summary>
internal class ParentBasedElseAlwaysRecordSampler : Sampler
{
    private const double DefaultSamplingProbabilityForRootSpan = 0.1;
    private readonly ParentBasedSampler parentBasedSampler;

    public ParentBasedElseAlwaysRecordSampler(double samplingProbabilityForRootSpan = DefaultSamplingProbabilityForRootSpan)
    {
        this.parentBasedSampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingProbabilityForRootSpan));
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // First, let's sample using the parentbased sampler.
        var samplingResult = this.parentBasedSampler.ShouldSample(samplingParameters);

        if (samplingResult.Decision != SamplingDecision.Drop)
        {
            // Parentbased sampler decided not to drop it, so we will sample this.
            return samplingResult;
        }

        // Parentbased sampler decided to drop it. We will return a RecordOnly
        // decision so that the span filtering processors later in the pipeline
        // can apply tailbased sampling rules (e.g., to sample all failed spans).
        // Returning a RecordOnly decision is relevant because:
        // 1. It causes the Processor pipeline to be invoked.
        // 2. It causes activity.IsAllDataRequested to return true, so most
        //    instrumentations end up populating the required attributes.
        return new SamplingResult(SamplingDecision.RecordOnly);
    }
}
