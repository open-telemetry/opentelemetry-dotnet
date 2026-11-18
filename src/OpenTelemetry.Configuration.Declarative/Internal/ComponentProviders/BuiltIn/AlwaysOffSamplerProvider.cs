// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the <c>always_off</c> sampler the schema defines.
/// </summary>
internal sealed class AlwaysOffSamplerProvider : PluginComponentProvider<Sampler>
{
    /// <summary>
    /// Gets the shared instance. The provider is stateless.
    /// </summary>
    internal static AlwaysOffSamplerProvider Instance { get; } = new();

    /// <inheritdoc/>
    public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(properties);
        Guard.ThrowIfNull(serviceProvider);

        // A new instance per node, so that shutting one down never affects a process-wide sampler.
        return new AlwaysOffSampler();
    }
}
