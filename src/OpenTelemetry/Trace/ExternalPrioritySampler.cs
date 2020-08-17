// <copyright file="ExternalPrioritySampler.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Trace;

public class ExternalPrioritySampler : Sampler
{
    private const string PriorityFlagName = "sampling.priority";
    private static readonly int PriorityFlagLength = "sampling.priority".Length;
    private static readonly IEnumerable<KeyValuePair<string, object>> EmptyAttributes = Enumerable.Empty<KeyValuePair<string, object>>();
    private readonly double probability;

    public ExternalPrioritySampler(double probability)
    {
        this.probability = probability;
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // todo: optimize for in-proc parent

        string tracestate = samplingParameters.ParentContext.TraceState;
        if (!this.TryGetPriority(tracestate, out var priority))
        {
            // if sampling.priority is NOT in the tracestate, generate a random one
            priority = (float)new Random().NextDouble();

            // prepend it to the tracestate
            tracestate = tracestate != null ?
                string.Concat(PriorityFlagName, '=', priority, ',', tracestate) :
                string.Concat(PriorityFlagName, '=', priority);
        }

        bool result = priority <= this.probability;
        return new SamplingResult(
            decision: result ? SamplingDecision.RecordAndSampled : SamplingDecision.NotRecord,
            attributes: result ? new[] { new KeyValuePair<string, object>(PriorityFlagName, priority) } : EmptyAttributes,
            tracestate: tracestate);
    }

    private bool TryGetPriority(string tracestate, out float upstreamPriority)
    {
        if (!string.IsNullOrEmpty(tracestate))
        {
            var start = tracestate.IndexOf(PriorityFlagName);
            if (start >= 0)
            {
                var end = tracestate.IndexOf(',', start);
                if (end < 0)
                {
                    end = tracestate.Length;
                }

                start += PriorityFlagLength + 1; // sampling.priority=
                if (float.TryParse(tracestate.Substring(start, end - start), out upstreamPriority))
                {
                    return true;
                }
            }
        }

        upstreamPriority = 0;
        return false;
    }
}
