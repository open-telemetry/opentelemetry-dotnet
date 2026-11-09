// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The providers for components the schema names and this package builds itself.
/// </summary>
/// <remarks>
/// Built-in providers are consulted before the registry, so they need no registration and a
/// registered provider cannot replace one.
/// </remarks>
internal static class BuiltInComponentProviders
{
    /// <summary>
    /// Returns the built-in provider for the span exporter named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The exporter name from the document.</param>
    /// <returns>The provider, or <see langword="null"/> when the name is not a built-in span exporter.</returns>
    internal static PluginComponentProvider<BaseExporter<Activity>>? FindSpanExporter(string name)
    {
        Guard.ThrowIfNull(name);

        return name switch
        {
            YamlKeys.Console => ConsoleSpanExporterProvider.Instance,
            _ => null,
        };
    }
}
