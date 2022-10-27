using System;

namespace OpenTelemetry.Trace;

public interface ITraceSamplerDetector
{
    string Name { get; }

    Sampler? Create(IServiceProvider serviceProvider, string? argument);
}
