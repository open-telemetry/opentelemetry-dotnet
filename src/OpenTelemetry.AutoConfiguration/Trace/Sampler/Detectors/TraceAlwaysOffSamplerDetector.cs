using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceAlwaysOffSamplerDetector : ITraceSamplerDetector
{
    public string Name => "always_off";

    public Sampler? Create(IServiceProvider serviceProvider, string? argument) => new AlwaysOnSampler();
}
