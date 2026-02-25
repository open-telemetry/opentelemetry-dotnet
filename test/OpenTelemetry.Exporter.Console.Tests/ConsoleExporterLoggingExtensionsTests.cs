// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

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
}
