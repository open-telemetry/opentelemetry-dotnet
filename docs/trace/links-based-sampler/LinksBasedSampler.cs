// <copyright file="LinksBasedSampler.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace LinksAndParentBasedSamplerExample;

/// <summary>
///  A non-probabilistic sampler that samples an activity if ANY of the linked activities
///  is sampled.
/// </summary>
internal class LinksBasedSampler : Sampler
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
