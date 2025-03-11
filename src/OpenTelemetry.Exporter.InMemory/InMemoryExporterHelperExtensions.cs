// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

public static class InMemoryExporterHelperExtensions
{
    /// <summary>
    /// Adds InMemory exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Activity"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddInMemoryExporter(this TracerProviderBuilder builder, ICollection<Activity> exportedItems)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(exportedItems);

#pragma warning disable CA2000 // Dispose objects before losing scope
        return builder.AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems)));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
