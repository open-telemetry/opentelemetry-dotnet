// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Trace;

/// <summary>
/// Trace sampler options.
/// <c>OTEL_TRACES_SAMPLER</c> and <c>OTEL_TRACES_SAMPLER_ARG</c> environment variables
/// are parsed during object construction.
/// </summary>
public sealed class SamplerOptions
{
    internal const string TracesSamplerConfigKey = "OTEL_TRACES_SAMPLER";
    internal const string TracesSamplerArgConfigKey = "OTEL_TRACES_SAMPLER_ARG";

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplerOptions"/> class.
    /// </summary>
    public SamplerOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal SamplerOptions(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(TracesSamplerConfigKey, out var type))
        {
            this.Type = type;
        }

        if (configuration.TryGetStringValue(TracesSamplerArgConfigKey, out var argument))
        {
            this.Argument = argument;

            if (TryParseTraceIdRatio(argument, out var traceIdRatio))
            {
                this.TraceIdRatio = traceIdRatio;
            }
        }
    }

    /// <summary>
    /// Gets or sets the sampler to use. When unset, the SDK falls back to
    /// <c>parentbased_always_on</c>. Supported values are <c>always_on</c>, <c>always_off</c>, <c>traceidratio</c>,
    /// <c>parentbased_always_on</c>, <c>parentbased_always_off</c>, and
    /// <c>parentbased_traceidratio</c>.
    /// </summary>
    /// <remarks>
    /// Note: A sampler set programmatically using
    /// <see cref="TracerProviderBuilderExtensions.SetSampler(TracerProviderBuilder, Sampler)"/>
    /// takes precedence over this value.
    /// </remarks>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the sampling probability, a value in the <c>[0.0, 1.0]</c> range.
    /// When unset or invalid, the SDK falls back to <c>1.0</c>. Only used when
    /// <see cref="Type"/> is <c>traceidratio</c> or <c>parentbased_traceidratio</c>.
    /// </summary>
    public double? TraceIdRatio { get; set; }

    /// <summary>
    /// Gets the verbatim <c>OTEL_TRACES_SAMPLER_ARG</c> value, retained so that an unusable value
    /// can be reported exactly as it was configured when the sampler uses it.
    /// </summary>
    internal string? Argument { get; }

    internal static bool TryParseTraceIdRatio(string value, out double traceIdRatio)
        => double.TryParse(
            value,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out traceIdRatio);
}
