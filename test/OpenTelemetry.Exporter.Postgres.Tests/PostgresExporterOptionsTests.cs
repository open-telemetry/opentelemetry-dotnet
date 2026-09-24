// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresExporterOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var options = new PostgresExporterOptions();

        Assert.Equal("Host=localhost;Port=5432;Database=opentelemetry;Username=postgres;Password=postgres", options.ConnectionString);
        Assert.Equal("otel", options.SchemaName);
        Assert.True(options.CreateSchema);
    }

    [Fact]
    public void CanSetConnectionString()
    {
        var options = new PostgresExporterOptions
        {
            ConnectionString = "Host=database;Database=test",
        };

        Assert.Equal("Host=database;Database=test", options.ConnectionString);
    }

    [Fact]
    public void CanSetSchemaName()
    {
        var options = new PostgresExporterOptions
        {
            SchemaName = "telemetry",
        };

        Assert.Equal("telemetry", options.SchemaName);
    }

    [Fact]
    public void CanDisableSchemaCreation()
    {
        var options = new PostgresExporterOptions
        {
            CreateSchema = false,
        };

        Assert.False(options.CreateSchema);
    }
}
