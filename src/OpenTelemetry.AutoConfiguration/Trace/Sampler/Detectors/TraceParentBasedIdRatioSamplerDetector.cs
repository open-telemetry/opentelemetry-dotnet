using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceParentBasedIdRatioSamplerDetector : ITraceSamplerDetector
{
    public string Name => "parentbased_traceidratio";

    public Sampler? Create(IServiceProvider serviceProvider, string? argument)
        => new ParentBasedSampler(TraceIdRatioBasedSamplerDetector.CreateTraceIdRatioBasedSampler(argument));
}
