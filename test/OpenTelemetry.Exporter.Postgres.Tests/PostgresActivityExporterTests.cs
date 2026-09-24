// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresActivityExporterTests
{
    [Fact]
    public void Constructor_ThrowsForNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new PostgresActivityExporter(null!));
    }

    [Fact]
    public void Constructor_ThrowsForInvalidSchemaName()
    {
        var options = new PostgresExporterOptions { SchemaName = "invalid;schema" };

        Assert.Throws<ArgumentException>(() => new PostgresActivityExporter(options));
    }

    [Fact]
    public void Export_ReturnsFailureWhenDatabaseUnavailable()
    {
        using var exporter = new PostgresActivityExporter(new PostgresExporterOptions
        {
            ConnectionString = "Host=127.0.0.1;Port=1;Database=missing;Timeout=1",
        });
        using var activity = new Activity("test");
        activity.Start();
        activity.Stop();

        var result = exporter.Export(new Batch<Activity>([activity], 1));

        Assert.Equal(ExportResult.Failure, result);
    }
}
