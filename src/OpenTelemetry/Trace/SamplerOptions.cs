// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for configuring the trace sampler.
/// OTEL_TRACES_SAMPLER and OTEL_TRACES_SAMPLER_ARG environment variables
/// are parsed during object construction.
/// </summary>
public sealed class SamplerOptions
{
    internal const string TracesSamplerConfigKey = "OTEL_TRACES_SAMPLER";
    internal const string TracesSamplerArgConfigKey = "OTEL_TRACES_SAMPLER_ARG";

    // Unlike some other options classes, we don't have a public parameterless constructor.
    // The internal-only constructor is the right long-term design for any new options class using
    // DelegatingOptionsFactory. Consumers never instantiate SamplerOptions directly, so there's no
    // need for a public constructor. The public constructor on BatchExportActivityProcessorOptions
    // pre-dates this pattern and is a historical artefact, not a template to follow for new work.

    private double? samplerArg;

    internal SamplerOptions(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(TracesSamplerConfigKey, out var samplerType))
        {
            this.SamplerType = samplerType;
        }

        if (configuration.TryGetStringValue(TracesSamplerArgConfigKey, out var samplerArgStr))
        {
            this.SamplerArgRaw = samplerArgStr;

            if (double.TryParse(
                samplerArgStr,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsedArg))
            {
                // Bypass the public setter so that SamplerArgRaw is not cleared.
                this.samplerArg = parsedArg;
            }

            // If unparsable, samplerArg stays null; SamplerArgRaw carries the bad string
            // for ReadTraceIdRatio to log when called for ratio-based samplers. This matches
            // the behavior prior to introducing this options type.
        }
    }

    /// <summary>
    /// Gets or sets the sampler type.
    /// </summary>
    public string? SamplerType { get; set; }

    /// <summary>
    /// Gets or sets the sampler argument.
    /// </summary>
    public double? SamplerArg
    {
        get => this.samplerArg;
        set
        {
            this.samplerArg = value;
            this.SamplerArgRaw = null; // no original config string when set programmatically
        }
    }

    /// <summary>
    /// Gets the original configuration string so it can be logged.
    /// </summary>
    internal string? SamplerArgRaw { get; private set; }
}
