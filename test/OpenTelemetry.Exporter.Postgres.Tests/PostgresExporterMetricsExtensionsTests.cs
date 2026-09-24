// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresExporterMetricsExtensionsTests
{
    [Fact]
    public void AddPostgresExporter_WithNoParameters_Success()
    {
        using var meter = new Meter(nameof(this.AddPostgresExporter_WithNoParameters_Success));
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddPostgresExporter()
            .Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddPostgresExporter_WithConfigureAction_Success()
    {
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddPostgresExporter(options => options.CreateSchema = false)
            .Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddPostgresExporter_ThrowsOnNullBuilder()
    {
        MeterProviderBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddPostgresExporter());
    }
}
