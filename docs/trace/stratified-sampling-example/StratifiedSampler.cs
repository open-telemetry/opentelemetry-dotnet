// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace StratifiedSamplingByQueryTypeDemo;

internal sealed class StratifiedSampler : Sampler
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
