// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Console.Tests;

#pragma warning disable CA1848 // Use the LoggerMessage delegates

public class ConsoleExporterLoggingExtensionsTests
{
    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNoParameters_Success()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) => options.AddConsoleExporter());
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleExporterLoggingExtensionsTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithConfigureAction_Success()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) =>
            {
                options.AddConsoleExporter((exporterOptions) =>
                {
                    exporterOptions.Targets = ConsoleExporterOutputTargets.Debug;
                });
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleExporterLoggingExtensionsTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithDifferentTargets_Success()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) =>
            {
                options.AddConsoleExporter((exporterOptions) =>
                {
                    exporterOptions.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug;
                });
            });
        });

        // Act
        var logger = loggerFactory.CreateLogger<ConsoleExporterLoggingExtensionsTests>();
        logger.LogInformation("Test message");

        // Assert
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddConsoleExporter_OpenTelemetryLoggerOptions_WithNoParameters_Success()
    {
        // Arrange
        var loggerOptions = new OpenTelemetryLoggerOptions();

        // Act
        var actual = loggerOptions.AddConsoleExporter();

        // Assert
        Assert.Same(loggerOptions, actual);
    }

    [Fact]
    public void AddConsoleExporter_OpenTelemetryLoggerOptions_WithConfigureAction_Success()
    {
        var loggerOptions = new OpenTelemetryLoggerOptions();

        // Act
        var actual = loggerOptions.AddConsoleExporter((options) =>
        {
            options.Targets = ConsoleExporterOutputTargets.Debug;
        });

        // Assert
        Assert.Same(loggerOptions, actual);
    }

    [Fact]
    public void AddConsoleExporter_OpenTelemetryLoggerOptions_WithNullOptions_Throws()
    {
        // Act and Assert
        OpenTelemetryLoggerOptions? loggerOptions = null;
        Assert.Throws<ArgumentNullException>(() => loggerOptions!.AddConsoleExporter());
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_ExportsLog_Success()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) =>
            {
                options.AddConsoleExporter((exporterOptions) =>
                {
                    exporterOptions.Targets = ConsoleExporterOutputTargets.Debug;
                });
            });
        });

        var logger = loggerFactory.CreateLogger<ConsoleExporterLoggingExtensionsTests>();

        // Act
        logger.LogInformation("Test information message");
        logger.LogWarning("Test warning message");
        logger.LogError("Test error message");

        // Assert
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_ThrowsOnNullBuilder()
    {
        // Act and Assert
        LoggerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter());
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNoParameters_UsesDefaultOptions()
    {
        // Arrange
        ConsoleExporterOutputTargets? targets = null;

        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices((services) => services.Configure<ConsoleExporterOptions>((options) => targets = options.Targets))
            .AddConsoleExporter()
            .Build();

        // Act
        var logger = loggerProvider.GetLogger(Utils.GetCurrentMethodName());
        logger.EmitLog(new LogRecordData { Body = "Test message" });

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Console, targets);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithConfigureAction_ExportsLog()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        var logger = loggerProvider.GetLogger(Utils.GetCurrentMethodName());
        logger.EmitLog(new LogRecordData { Body = "Test message" });

        // Assert
        Assert.Contains(BodyLine("Test message"), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNameAndConfigureAction_ExportsLog()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddConsoleExporter("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        var logger = loggerProvider.GetLogger(Utils.GetCurrentMethodName());
        logger.EmitLog(new LogRecordData { Body = "Test message" });

        // Assert
        Assert.Contains(BodyLine("Test message"), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNameAndNullConfigureAction_UsesNamedOptions()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices((services) =>
            {
                services.Configure<ConsoleExporterOptions>((options) => options.Targets = ConsoleExporterOutputTargets.Console);
                services.Configure<ConsoleExporterOptions>("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug);
            })
            .AddConsoleExporter("custom-name", configure: null)
            .Build();

        // Act
        var logger = loggerProvider.GetLogger(Utils.GetCurrentMethodName());
        logger.EmitLog(new LogRecordData { Body = "Test message" });

        // Assert
        Assert.Contains(BodyLine("Test message"), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithConfigureAction_ThrowsOnNullBuilder()
    {
        // Act and Assert
        LoggerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter((options) => { }));
    }

    private static string BodyLine(string body)
        => $"{"LogRecord.Body:",-35}{body}";
}
