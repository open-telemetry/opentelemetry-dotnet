// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs;

/// <summary>PostgreSQL log exporter registration extensions.</summary>
public static class PostgresLogExporterExtensions
{
    /// <summary>Adds the PostgreSQL exporter to logger options.</summary>
    /// <param name="loggerOptions">The logger options to configure.</param>
    /// <param name="configure">An optional callback for configuring exporter options.</param>
    /// <returns>The supplied logger options to chain calls.</returns>
    public static OpenTelemetryLoggerOptions AddPostgresExporter(this OpenTelemetryLoggerOptions loggerOptions, Action<PostgresExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(loggerOptions);

        var options = new PostgresExporterOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000
        return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(new PostgresLogRecordExporter(options)));
#pragma warning restore CA2000
    }

    /// <summary>Adds the PostgreSQL exporter to the logger provider.</summary>
    /// <param name="builder">The logger provider builder to configure.</param>
    /// <param name="configure">An optional callback for configuring exporter options.</param>
    /// <returns>The supplied logger provider builder to chain calls.</returns>
    public static LoggerProviderBuilder AddPostgresExporter(this LoggerProviderBuilder builder, Action<PostgresExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PostgresExporterOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000
        return builder.AddProcessor(new SimpleLogRecordExportProcessor(new PostgresLogRecordExporter(options)));
#pragma warning restore CA2000
    }
}
