// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class SamplerFactory
{
    internal static Sampler GetSampler(SamplerOptions options, Sampler? stateSampler)
    {
        var sampler = stateSampler;

        if (options.Type is string samplerType && !string.IsNullOrWhiteSpace(samplerType))
        {
            if (sampler != null)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
                    $"Trace sampler configuration value '{samplerType}' has been ignored because a value '{sampler.GetType().FullName}' was set programmatically.");
                return sampler;
            }

            switch (samplerType)
            {
                case var _ when string.Equals(samplerType, SamplerOptions.AlwaysOnType, StringComparison.OrdinalIgnoreCase):
                    sampler = AlwaysOnSampler.Instance;
                    break;
                case var _ when string.Equals(samplerType, SamplerOptions.AlwaysOffType, StringComparison.OrdinalIgnoreCase):
                    sampler = AlwaysOffSampler.Instance;
                    break;
                case var _ when string.Equals(samplerType, SamplerOptions.TraceIdRatioType, StringComparison.OrdinalIgnoreCase):
                    sampler = new TraceIdRatioBasedSampler(ReadTraceIdRatio(options));
                    break;
                case var _ when string.Equals(samplerType, SamplerOptions.ParentBasedAlwaysOnType, StringComparison.OrdinalIgnoreCase):
                    sampler = new ParentBasedSampler(AlwaysOnSampler.Instance);
                    break;
                case var _ when string.Equals(samplerType, SamplerOptions.ParentBasedAlwaysOffType, StringComparison.OrdinalIgnoreCase):
                    sampler = new ParentBasedSampler(AlwaysOffSampler.Instance);
                    break;
                case var _ when string.Equals(samplerType, SamplerOptions.ParentBasedTraceIdRatioType, StringComparison.OrdinalIgnoreCase):
                    sampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(ReadTraceIdRatio(options)));
                    break;
                default:
                    OpenTelemetrySdkEventSource.Log.TracesSamplerConfigInvalid(samplerType);
                    break;
            }

            if (sampler != null)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Trace sampler set to '{sampler.GetType().FullName}' from configuration.");
            }
        }

        return sampler ?? new ParentBasedSampler(AlwaysOnSampler.Instance);
    }

    private static double ReadTraceIdRatio(SamplerOptions options)
    {
        var traceIdRatio = options.TraceIdRatio;

        if (traceIdRatio is null && options.Argument is string arg
            && SamplerOptions.TryParseTraceIdRatio(arg, out var fromArg))
        {
            traceIdRatio = fromArg;
        }

        if (traceIdRatio is double ratio
            && !double.IsNaN(ratio)
            && !double.IsInfinity(ratio)
            && ratio >= 0.0
            && ratio <= 1.0)
        {
            return ratio;
        }

        OpenTelemetrySdkEventSource.Log.TracesSamplerArgConfigInvalid(DescribeUnusableTraceIdRatio(options, traceIdRatio));

        return 1.0;
    }

    private static string DescribeUnusableTraceIdRatio(SamplerOptions options, double? effectiveRatio)
    {
        var argument = options.Argument;

        if (effectiveRatio is not double ratio)
        {
            // The configuration string could not be parsed, so it is the only value to report.
            return argument ?? string.Empty;
        }

        return argument != null
            && SamplerOptions.TryParseTraceIdRatio(argument, out var parsed)
            && parsed.Equals(ratio)
                ? argument
                : ratio.ToString(CultureInfo.InvariantCulture);
    }
}
