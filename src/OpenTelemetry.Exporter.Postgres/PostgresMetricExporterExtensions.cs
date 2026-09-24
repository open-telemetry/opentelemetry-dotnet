// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Metrics;

/// <summary>PostgreSQL metric exporter registration extensions.</summary>
public static class PostgresMetricExporterExtensions
{
    /// <summary>Adds the PostgreSQL exporter to the meter provider.</summary>
    /// <param name="builder">The meter provider builder to configure.</param>
    /// <param name="configure">An optional callback for configuring exporter options.</param>
    /// <returns>The supplied meter provider builder to chain calls.</returns>
    public static MeterProviderBuilder AddPostgresExporter(this MeterProviderBuilder builder, Action<PostgresExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PostgresExporterOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000
        return builder.AddReader(sp => new BaseExportingMetricReader(new PostgresMetricExporter(options)));
#pragma warning restore CA2000
    }
}
