// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the <c>trace_id_ratio_based</c> sampler the schema defines.
/// </summary>
internal sealed class TraceIdRatioBasedSamplerProvider : PluginComponentProvider<Sampler>
{
    /// <summary>
    /// Gets the shared instance. The provider is stateless.
    /// </summary>
    internal static TraceIdRatioBasedSamplerProvider Instance { get; } = new();

    /// <inheritdoc/>
    public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(properties);
        Guard.ThrowIfNull(serviceProvider);

        var ratio = properties.GetDouble(YamlKeys.Ratio);

        return ratio.Outcome switch
        {
            ConfigValueOutcome.Absent or ConfigValueOutcome.PresentNull => new TraceIdRatioBasedSampler(1),
            ConfigValueOutcome.Present when ratio.Value is >= 0 and <= 1 => new TraceIdRatioBasedSampler(ratio.Value),
            _ => throw BindingHelpers.CreateException(
                YamlPath.Child(YamlKeys.TraceIdRatioBased, YamlKeys.Ratio),
                ratio.Position,
                "must be a number from 0 to 1."),
        };
    }
}
