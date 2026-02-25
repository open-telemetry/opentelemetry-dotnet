// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA1873 // Avoid potentially expensive logging

public class ConsoleLogRecordExporterTests
{
    [Fact]
    public void Export_Success()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("Hello, World!");

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithTraceContext()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();

        // Act
        using var activity = new Activity("test");
        activity.Start();

        logger.LogInformation("Log with trace context");

        activity.Stop();

        // Assert
        var logRecord = Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([logRecord], 1));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithEventId()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation(new EventId(42, "TestEvent"), "Test message");

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithException()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();

        // Act
        var exception = new InvalidOperationException("Test exception");
        logger.LogError(exception, "Error occurred");

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithScopes()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(records);
            });
        });

        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();

        // Act
        using (logger.BeginScope("Scope1"))
        using (logger.BeginScope("Scope2"))
        {
            logger.LogInformation("Message with scopes");
        }

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithAttributes()
    {
        // Arrange
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddInMemoryExporter(logRecords);
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("User {UserId} logged in from {IpAddress}", 123, "192.168.1.1");

        // Assert
        Assert.Single(logRecords);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. logRecords], logRecords.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_AfterDispose_ReturnsFailure()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.Single(records);

        // Act
        var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        exporter.Dispose();

        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Failure, actual);

        // Second export should still return failure
        actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));
        Assert.Equal(ExportResult.Failure, actual);
    }

    [Fact]
    public void Export_WithDifferentSeverityLevels()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();

        // Act
        logger.LogTrace("Trace message");
        logger.LogDebug("Debug message");
        logger.LogInformation("Info message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");
        logger.LogCritical("Critical message");

        // Assert
        Assert.Equal(6, records.Count);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithResource()
    {
        // Arrange
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("TestService", serviceVersion: "1.0.0"));
                options.AddInMemoryExporter(logRecords);
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("Test message with resource");

        // Assert
        Assert.Single(logRecords);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<LogRecord>([.. logRecords], logRecords.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithDebugTarget()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Debug,
        });
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithBothTargets()
    {
        // Arrange
        var records = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddInMemoryExporter(records));
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleLogRecordExporterTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.Single(records);

        // Act
        using var exporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug,
        });
        var actual = exporter.Export(new Batch<LogRecord>([.. records], records.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }
}
