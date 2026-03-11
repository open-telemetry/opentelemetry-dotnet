// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleMetricExporterTests
{
    [Fact]
    public void Export_Counter_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_Gauge_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        meter.CreateObservableGauge("test-gauge", () => 42.5);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_Histogram_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var histogram = meter.CreateHistogram<double>("test-histogram");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        histogram.Record(10.5);
        histogram.Record(20.3);
        histogram.Record(30.1);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_ExponentialHistogram_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var histogram = meter.CreateHistogram<double>("test-exponential-histogram");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddView("test-exponential-histogram", new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        histogram.Record(10.5);
        histogram.Record(20.3);
        histogram.Record(30.1);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithTags()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter-with-tags");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100, new KeyValuePair<string, object?>("tag1", "value1"));
        counter.Add(200, new KeyValuePair<string, object?>("tag2", "value2"));
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithDescription()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter-with-description", unit: "bytes", description: "Test counter description");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithMeterVersion()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName, "1.0.0");
        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithResource()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("TestService", serviceVersion: "1.0.0"))
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_LongCounter_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-long-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100L);
        counter.Add(200L);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_DoubleCounter_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<double>("test-double-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100.5);
        counter.Add(200.3);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithDebugTarget()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Debug,
        });
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_WithBothTargets()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug,
        });
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_MultipleMetrics_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter1 = meter.CreateCounter<long>("test-counter-1");
        var counter2 = meter.CreateCounter<long>("test-counter-2");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        counter1.Add(100);
        counter2.Add(200);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Equal(2, metrics.Count);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }

    [Fact]
    public void Export_HistogramWithMinMax_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var histogram = meter.CreateHistogram<double>("test-histogram-minmax");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        // Act
        histogram.Record(5.0);
        histogram.Record(10.0);
        histogram.Record(15.0);
        meterProvider!.ForceFlush();

        // Assert
        Assert.Single(metrics);

        // Act
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions());
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);
    }
}
