// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresLogRecordExporterTests
{
    [Fact]
    public void Constructor_ThrowsForEmptyConnectionString()
    {
        var options = new PostgresExporterOptions { ConnectionString = string.Empty };

        Assert.Throws<ArgumentException>(() => new PostgresLogRecordExporter(options));
    }

    [Fact]
    public void Export_ReturnsFailureWhenDatabaseUnavailable()
    {
        using var exporter = new PostgresLogRecordExporter(new PostgresExporterOptions
        {
            ConnectionString = "Host=127.0.0.1;Port=1;Database=missing;Timeout=1",
        });

        var result = exporter.Export(new Batch<LogRecord>(Array.Empty<LogRecord>(), 0));

        Assert.Equal(ExportResult.Failure, result);
    }
}
