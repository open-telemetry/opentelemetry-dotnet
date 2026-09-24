// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Trace;

/// <summary>Extension methods to simplify registering the PostgreSQL exporter.</summary>
public static class PostgresExporterHelperExtensions
{
    /// <summary>Adds the PostgreSQL exporter to the <see cref="TracerProviderBuilder"/>.</summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> to chain calls.</returns>
    public static TracerProviderBuilder AddPostgresExporter(this TracerProviderBuilder builder, Action<PostgresExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PostgresExporterOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000
        return builder.AddProcessor(new SimpleActivityExportProcessor(new PostgresActivityExporter(options)));
#pragma warning restore CA2000
    }
}