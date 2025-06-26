// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace LinksAndParentBasedSamplerExample;

/// <summary>
///  A non-probabilistic sampler that samples an activity if ANY of the linked activities
///  is sampled.
/// </summary>
internal sealed class LinksBasedSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.Links != null)
        {
            foreach (var activityLink in samplingParameters.Links)
            {
                if ((activityLink.Context.TraceFlags &
                      ActivityTraceFlags.Recorded) != 0)
                {
                    // If any linked activity is sampled, we will include this activity as well.
                    Console.WriteLine($"{samplingParameters.TraceId}: At least one linked activity (TraceID: {activityLink.Context.TraceId}, SpanID: {activityLink.Context.SpanId}) is sampled. Hence, LinksBasedSampler decision is RecordAndSample");
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                }
            }
        }

        // There are either no linked activities or none of them are sampled.
        // Hence, we will drop this activity.
        Console.WriteLine($"{samplingParameters.TraceId}: No linked span is sampled. Hence, LinksBasedSampler decision is Drop.");
        return new SamplingResult(SamplingDecision.Drop);
    }
}
