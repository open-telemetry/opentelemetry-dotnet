using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceParentBasedAlwaysOffSamplerDetector : ITraceSamplerDetector
{
    public string Name => "parentbased_always_off";

    public Sampler? Create(IServiceProvider serviceProvider, string? argument) => new ParentBasedSampler(new AlwaysOffSampler());
}
