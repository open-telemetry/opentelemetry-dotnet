using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceParentBasedAlwaysOnSamplerDetector : ITraceSamplerDetector
{
    public string Name => "parentbased_always_on";

    public Sampler? Create(IServiceProvider serviceProvider, string? argument) => new ParentBasedSampler(new AlwaysOnSampler());
}
