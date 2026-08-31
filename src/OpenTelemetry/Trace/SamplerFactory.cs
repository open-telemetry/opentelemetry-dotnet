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

#pragma warning disable CA1308 // Normalize strings to uppercase
            var normalizedType = samplerType.Trim().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            sampler = normalizedType switch
            {
                SamplerOptions.AlwaysOnType => AlwaysOnSampler.Instance,
                SamplerOptions.AlwaysOffType => AlwaysOffSampler.Instance,
                SamplerOptions.TraceIdRatioType => new TraceIdRatioBasedSampler(ReadTraceIdRatio(options)),
                SamplerOptions.ParentBasedAlwaysOnType => new ParentBasedSampler(AlwaysOnSampler.Instance),
                SamplerOptions.ParentBasedAlwaysOffType => new ParentBasedSampler(AlwaysOffSampler.Instance),
                SamplerOptions.ParentBasedTraceIdRatioType =>
                    new ParentBasedSampler(new TraceIdRatioBasedSampler(ReadTraceIdRatio(options))),
                _ => null,
            };

            if (sampler is null)
            {
                // The unrecognized value is reported exactly as it was configured.
                OpenTelemetrySdkEventSource.Log.TracesSamplerConfigInvalid(samplerType);
            }
            else
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

        // NaN fails both relational patterns, which is required because TraceIdRatio is a
        // settable property and can be assigned NaN programmatically even though parsing rejects it.
        if (traceIdRatio is double ratio and >= 0.0 and <= 1.0)
        {
            return ratio;
        }

        if (options.Argument != null || options.TraceIdRatio != null)
        {
            // Only report a diagnostic when a value was actually configured. The specification
            // requires the default of 1.0 to be used when no argument is set.
            OpenTelemetrySdkEventSource.Log.TracesSamplerArgConfigInvalid(DescribeUnusableTraceIdRatio(options));
        }

        return 1.0;
    }

    private static string DescribeUnusableTraceIdRatio(SamplerOptions options) =>
        options.Argument is string argument
            && options.TraceIdRatio == options.ConfiguredTraceIdRatio
                ? argument // verbatim configured string, including trailing zero or separator
                : options.TraceIdRatio?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
