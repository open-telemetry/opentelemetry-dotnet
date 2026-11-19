// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class SamplerFactory
{
    internal static Sampler GetSampler(IServiceProvider serviceProvider, Sampler? stateSampler)
    {
        if (stateSampler != null)
        {
            // A sampler set programmatically wins, so the options are deliberately not resolved
            // avoiding binding and validation, which must not be able to fail a provider whose
            // sampler is already registered programmatically via SetSampler.
            if (serviceProvider.GetRequiredService<IConfiguration>()
                    .TryGetStringValue(SamplerOptions.TracesSamplerConfigKey, out var configuredType))
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
                    $"Trace sampler configuration value '{configuredType}' has been ignored because a value '{stateSampler.GetType().FullName}' was set programmatically.");
            }

            return stateSampler;
        }

        return GetSampler(serviceProvider.GetRequiredService<IOptions<SamplerOptions>>().Value);
    }

    private static Sampler GetSampler(SamplerOptions options)
    {
        Sampler? sampler = null;

        if (options.Type is string samplerType && !string.IsNullOrWhiteSpace(samplerType))
        {
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
        // NaN fails both relational patterns, which is required because TraceIdRatio is a
        // settable property and can be assigned NaN programmatically even though parsing rejects it.
        if (options.TraceIdRatio is double ratio and >= 0.0 and <= 1.0)
        {
            return ratio;
        }

        // Only report a diagnostic when an unusable value is in effect. The specification requires
        // the default of 1.0 to be used when no argument is set, and a ratio that was cleared
        // programmatically is unset rather than unusable.
        if (options.TraceIdRatio != null
            || (options.Argument != null && options.ConfiguredTraceIdRatio is null))
        {
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
