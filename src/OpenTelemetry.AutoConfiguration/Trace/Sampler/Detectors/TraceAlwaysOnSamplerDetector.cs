using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceAlwaysOnSamplerDetector : ITraceSamplerDetector
{
    public string Name => "always_on";

    public Sampler? Create(IServiceProvider serviceProvider, string? argument) => new AlwaysOnSampler();
}
