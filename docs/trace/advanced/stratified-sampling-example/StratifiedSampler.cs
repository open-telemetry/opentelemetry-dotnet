// <copyright file="StratifiedSampler.cs" company="OpenTelemetry Authors">
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

namespace StratifiedSamplingByQueryTypeDemo;

internal class StratifiedSampler : Sampler
{
    // For this POC, we have two groups.
    // 0 is the group corresponding to user-initiated queries where we want a 100% sampling rate.
    // 1 is the group corresponding to programmatic queries where we want a lower sampling rate, say 10%
    private const int NumGroups = 2;
    private const string QueryTypeTag = "queryType";
    private const string QueryTypeUserInitiated = "userInitiated";
    private const string QueryTypeProgrammatic = "programmatic";

    private readonly Dictionary<int, double> samplingRatios = new();
    private readonly List<Sampler> samplers = new();

    public StratifiedSampler()
    {
        // Initialize sampling ratios for different groups
        this.samplingRatios[0] = 1.0;
        this.samplingRatios[1] = 0.2;

        for (var i = 0; i < NumGroups; i++)
        {
            this.samplers.Add(new TraceIdRatioBasedSampler(this.samplingRatios[i]));
        }
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.Tags != null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key.Equals(QueryTypeTag, StringComparison.OrdinalIgnoreCase))
                {
                    var queryType = tag.Value as string;
                    if (queryType == null)
                    {
                        continue;
                    }

                    if (queryType.Equals(QueryTypeUserInitiated, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"StratifiedSampler handling userinitiated query");
                        return this.samplers[0].ShouldSample(samplingParameters);
                    }
                    else if (queryType.Equals(QueryTypeProgrammatic, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"StratifiedSampler handling programmatic query");
                        return this.samplers[1].ShouldSample(samplingParameters);
                    }
                    else
                    {
                        Console.WriteLine("Unexpected query type");
                    }
                }
            }
        }

        return new SamplingResult(SamplingDecision.Drop);
    }
}
