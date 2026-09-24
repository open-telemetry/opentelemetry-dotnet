// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresMetricExporterTests
{
    [Fact]
    public void Constructor_ThrowsForInvalidSchemaName()
    {
        var options = new PostgresExporterOptions { SchemaName = "invalid.schema" };

        Assert.Throws<ArgumentException>(() => new PostgresMetricExporter(options));
    }

    [Fact]
    public void Export_ReturnsFailureWhenDatabaseUnavailable()
    {
        using var exporter = new PostgresMetricExporter(new PostgresExporterOptions
        {
            ConnectionString = "Host=127.0.0.1;Port=1;Database=missing;Timeout=1",
        });

        var result = exporter.Export(new Batch<Metric>(Array.Empty<Metric>(), 0));

        Assert.Equal(ExportResult.Failure, result);
    }
}
