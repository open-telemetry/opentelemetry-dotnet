using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceIdRatioBasedSamplerDetector : ITraceSamplerDetector
{
    public string Name => "traceidratio";

    public static Sampler CreateTraceIdRatioBasedSampler(string? argument)
    {
        const double defaultRatio = 1.0;

        double ratio = defaultRatio;

        if (!string.IsNullOrWhiteSpace(argument)
            && double.TryParse(argument, out var parsedRatio)
            && parsedRatio >= 0.0
            && parsedRatio <= 1.0)
        {
            ratio = parsedRatio;
        }

        return new TraceIdRatioBasedSampler(ratio);
    }

    public Sampler? Create(IServiceProvider serviceProvider, string? argument) => CreateTraceIdRatioBasedSampler(argument);
}
