// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public class PostgresExporterLoggingExtensionsTests
{
    [Fact]
    public void AddPostgresExporter_OpenTelemetryLoggerOptions_WithNoParameters_Success()
    {
        var options = new OpenTelemetryLoggerOptions();

        var actual = options.AddPostgresExporter();

        Assert.Same(options, actual);
    }

    [Fact]
    public void AddPostgresExporter_LoggerProviderBuilder_WithNoParameters_Success()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddPostgresExporter());
        });

        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddPostgresExporter_OpenTelemetryLoggerOptions_WithConfigureAction_Success()
    {
        var options = new OpenTelemetryLoggerOptions();

        var actual = options.AddPostgresExporter(exporterOptions => exporterOptions.CreateSchema = false);

        Assert.Same(options, actual);
    }

    [Fact]
    public void AddPostgresExporter_ThrowsOnNullLoggerOptions()
    {
        OpenTelemetryLoggerOptions? options = null;

        Assert.Throws<ArgumentNullException>(() => options!.AddPostgresExporter());
    }

    [Fact]
    public void AddPostgresExporter_ThrowsOnNullBuilder()
    {
        LoggerProviderBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddPostgresExporter());
    }
}
