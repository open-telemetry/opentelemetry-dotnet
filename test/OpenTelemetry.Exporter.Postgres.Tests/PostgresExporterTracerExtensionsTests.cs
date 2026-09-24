// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresExporterTracerExtensionsTests
{
    [Fact]
    public void AddPostgresExporter_WithNoParameters_Success()
    {
        using var source = new ActivitySource(nameof(this.AddPostgresExporter_WithNoParameters_Success));
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddPostgresExporter()
            .Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddPostgresExporter_WithConfigureAction_Success()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddPostgresExporter(options =>
            {
                options.ConnectionString = "Host=database;Database=test";
                options.SchemaName = "telemetry";
                options.CreateSchema = false;
            })
            .Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddPostgresExporter_ThrowsOnNullBuilder()
    {
        TracerProviderBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddPostgresExporter());
    }
}
