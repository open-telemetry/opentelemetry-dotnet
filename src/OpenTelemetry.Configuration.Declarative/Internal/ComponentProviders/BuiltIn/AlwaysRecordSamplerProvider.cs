// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the <c>always_record</c> sampler the schema defines.
/// </summary>
internal sealed class AlwaysRecordSamplerProvider : PluginComponentProvider<Sampler>
{
    /// <summary>
    /// Gets the shared instance. The provider is stateless.
    /// </summary>
    internal static AlwaysRecordSamplerProvider Instance { get; } = new();

    /// <inheritdoc/>
    public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(properties);
        Guard.ThrowIfNull(serviceProvider);

        var root = SamplerFactory.CreateChild(properties, YamlKeys.AlwaysRecord, YamlKeys.Root, serviceProvider)
            ?? throw BindingHelpers.CreateException(YamlKeys.AlwaysRecord, default, $"requires '{YamlKeys.Root}', a sampler.");

        return new AlwaysRecordSampler(root);
    }
}
