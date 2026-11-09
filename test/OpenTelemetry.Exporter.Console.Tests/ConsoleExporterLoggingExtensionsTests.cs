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

        using var serviceProvider = BuildServiceProvider(
            (services) => services.Configure<ConsoleExporterOptions>((options) => targets = options.Targets),
            (builder) => builder.AddConsoleExporter());

        // Act
        LogTestMessage(serviceProvider);

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Console, targets);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithConfigureAction_ExportsLog()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var serviceProvider = BuildServiceProvider(
            configureServices: null,
            (builder) => builder.AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug));

        // Act
        LogTestMessage(serviceProvider);

        // Assert
        Assert.Contains(GetTestMessageBodyLine(), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNameAndConfigureAction_ExportsLog()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var serviceProvider = BuildServiceProvider(
            configureServices: null,
            (builder) => builder.AddConsoleExporter("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug));

        // Act
        LogTestMessage(serviceProvider);

        // Assert
        Assert.Contains(GetTestMessageBodyLine(), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithNameAndNullConfigureAction_UsesNamedOptions()
    {
        // Arrange
        using var capture = new TraceOutputCapture();
        using var serviceProvider = BuildServiceProvider(
            (services) =>
            {
                services.Configure<ConsoleExporterOptions>((options) => options.Targets = ConsoleExporterOutputTargets.Console);
                services.Configure<ConsoleExporterOptions>("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug);
            },
            (builder) => builder.AddConsoleExporter("custom-name", configure: null));

        // Act
        LogTestMessage(serviceProvider);

        // Assert
        Assert.Contains(GetTestMessageBodyLine(), capture.Lines);
    }

    [Fact]
    public void AddConsoleExporter_LoggerProviderBuilder_WithConfigureAction_ThrowsOnNullBuilder()
    {
        // Act and Assert
        LoggerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter((options) => { }));
    }

    private static string GetTestMessageBodyLine()
        => $"{"LogRecord.Body:",-35}Test message";

    private static ServiceProvider BuildServiceProvider(
        Action<IServiceCollection>? configureServices,
        Action<LoggerProviderBuilder> configure)
    {
        var services = new ServiceCollection();

        configureServices?.Invoke(services);

        services.AddLogging((builder) => builder.AddOpenTelemetry());
        services.ConfigureOpenTelemetryLoggerProvider(configure);

        return services.BuildServiceProvider();
    }

    private static void LogTestMessage(ServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(Utils.GetCurrentMethodName());
        logger.LogInformation("Test message");
    }
}
