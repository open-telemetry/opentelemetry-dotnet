// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleExporterMetricsExtensionsTests
{
    [Fact]
    public void AddConsoleExporter_WithNoParameters_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter()
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithConfigureAction_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndConfigureAction_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndNullConfigure_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter("custom-name", (Action<ConsoleExporterOptions>?)null)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_ExportsMetric_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_ThrowsOnNullBuilder()
    {
        // Act and Assert
        MeterProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter());
    }

    [Fact]
    public void AddConsoleExporter_WithExporterAndMetricReaderOptions_ConfiguresBothAndExports()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        ConsoleExporterOptions? exporterOptions = null;
        MetricReaderOptions? metricReaderOptions = null;

        using var capture = new TraceOutputCapture();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter((exporter, metricReader) =>
            {
                exporterOptions = exporter;
                metricReaderOptions = metricReader;

                exporter.Targets = ConsoleExporterOutputTargets.Debug;
                metricReader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite;
            })
            .Build();

        // Act
        counter.Add(100);
        var flushed = meterProvider!.ForceFlush();

        // Assert
        Assert.True(flushed);
        Assert.NotNull(exporterOptions);
        Assert.NotNull(metricReaderOptions);
        Assert.Contains(capture.Lines, (p) => p.Contains("Metric Name: test-counter", StringComparison.Ordinal));
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndExporterAndMetricReaderOptions_UsesNamedOptions()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        ConsoleExporterOptions? exporterOptions = null;

        using var capture = new TraceOutputCapture();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .ConfigureServices((services) =>
            {
                services.Configure<ConsoleExporterOptions>((options) => options.Targets = ConsoleExporterOutputTargets.Console);
                services.Configure<ConsoleExporterOptions>("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug);
            })
            .AddConsoleExporter("custom-name", (exporter, metricReader) =>
            {
                exporterOptions = exporter;

                // Only collect on demand so that the output is always written by ForceFlush() on this thread
                metricReader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite;
            })
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.NotNull(exporterOptions);
        Assert.Equal(ConsoleExporterOutputTargets.Debug, exporterOptions.Targets);
        Assert.Contains(capture.Lines, (p) => p.Contains("Metric Name: test-counter", StringComparison.Ordinal));
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndNullExporterAndMetricReaderOptions_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var capture = new TraceOutputCapture();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .ConfigureServices((services) =>
            {
                services.Configure<ConsoleExporterOptions>("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Debug);

                // Only collect on demand so that the output is always written by ForceFlush() on this thread
                services.Configure<MetricReaderOptions>("custom-name", (options) => options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite);
            })
            .AddConsoleExporter("custom-name", (Action<ConsoleExporterOptions, MetricReaderOptions>?)null)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Contains(capture.Lines, (p) => p.Contains("Metric Name: test-counter", StringComparison.Ordinal));
    }

    [Fact]
    public void AddConsoleExporter_WithExporterAndMetricReaderOptions_ThrowsOnNullBuilder()
    {
        // Act and Assert
        MeterProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter((Action<ConsoleExporterOptions, MetricReaderOptions>)((exporter, metricReader) => { })));
    }
}
