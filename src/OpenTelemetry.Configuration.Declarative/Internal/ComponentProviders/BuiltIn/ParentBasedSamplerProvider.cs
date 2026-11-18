// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the <c>parent_based</c> sampler the schema defines.
/// </summary>
internal sealed class ParentBasedSamplerProvider : PluginComponentProvider<Sampler>
{
    /// <summary>
    /// Gets the shared instance. The provider is stateless.
    /// </summary>
    internal static ParentBasedSamplerProvider Instance { get; } = new();

    /// <inheritdoc/>
    public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(properties);
        Guard.ThrowIfNull(serviceProvider);

        var created = new List<Sampler>(5);

        try
        {
            // An omitted root is always_on. Core applies its own defaults to the other children.
            var root = CreateChild(YamlKeys.Root) ?? Track(new AlwaysOnSampler());

            return new ParentBasedSampler(
                root,
                remoteParentSampled: CreateChild(YamlKeys.RemoteParentSampled),
                remoteParentNotSampled: CreateChild(YamlKeys.RemoteParentNotSampled),
                localParentSampled: CreateChild(YamlKeys.LocalParentSampled),
                localParentNotSampled: CreateChild(YamlKeys.LocalParentNotSampled));
        }
        catch
        {
            foreach (var sampler in created)
            {
                ComponentCleanup.ShutDownAndDispose(sampler);
            }

            throw;
        }

        Sampler Track(Sampler sampler)
        {
            created.Add(sampler);
            return sampler;
        }

        Sampler? CreateChild(string key) =>
            SamplerFactory.CreateChild(properties, YamlKeys.ParentBased, key, serviceProvider) is { } child
                ? Track(child)
                : null;
    }
}
