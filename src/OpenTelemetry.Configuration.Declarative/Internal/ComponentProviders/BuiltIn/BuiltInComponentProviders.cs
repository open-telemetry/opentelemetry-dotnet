// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Registers providers for components the schema names and this package builds itself.
/// </summary>
internal static class BuiltInComponentProviders
{
    /// <summary>
    /// Registers the built-in providers using the same mechanism as other component providers.
    /// </summary>
    /// <param name="services">The services to register into.</param>
    internal static void Register(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services.AddPluginComponentProvider(YamlKeys.Console, ConsoleSpanExporterProvider.Instance);
        services.AddPluginComponentProvider(YamlKeys.AlwaysOff, AlwaysOffSamplerProvider.Instance);
        services.AddPluginComponentProvider(YamlKeys.AlwaysOn, AlwaysOnSamplerProvider.Instance);
        services.AddPluginComponentProvider(YamlKeys.AlwaysRecord, AlwaysRecordSamplerProvider.Instance);
        services.AddPluginComponentProvider(YamlKeys.ParentBased, ParentBasedSamplerProvider.Instance);
        services.AddPluginComponentProvider(YamlKeys.TraceIdRatioBased, TraceIdRatioBasedSamplerProvider.Instance);
    }
}
