// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Creates the <c>console</c> span exporter the schema defines.
/// </summary>
/// <remarks>
/// The only file in the package that references the console exporter's types, so that the mapping
/// can move to another package without changes.
/// </remarks>
internal sealed class ConsoleSpanExporterProvider : PluginComponentProvider<BaseExporter<Activity>>
{
    /// <summary>
    /// Gets the shared instance. The provider is stateless.
    /// </summary>
    internal static ConsoleSpanExporterProvider Instance { get; } = new();

    /// <inheritdoc/>
    public override BaseExporter<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(properties);
        Guard.ThrowIfNull(serviceProvider);

        // The schema defines no properties for the console exporter. The exporter uses fresh
        // options, unaffected by options configured in dependency injection.
        return new ConsoleActivityExporter(new ConsoleExporterOptions());
    }
}
