// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;

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

    [Fact]
    public void Export_MeterWithVersionSchemaUrlAndTags_WritesInstrumentationScope()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(new MeterOptions(meterName)
        {
            Version = "1.2.3",
            TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.40.0",
            Tags =
            [
                new KeyValuePair<string, object?>("meter.string", "value"),
                new KeyValuePair<string, object?>("meter.number", 42),
            ],
        });

        var counter = meter.CreateCounter<long>("test-counter");

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(100);
        meterProvider!.ForceFlush();

        Assert.Single(metrics);

        // Act
        using var capture = new TraceOutputCapture();
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions { Targets = ConsoleExporterOutputTargets.Debug });
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);

        var scope = capture.Lines.SkipWhile((p) => p != "Instrumentation scope (Meter):").Take(7).ToArray();
        Assert.Equal(
            [
                "Instrumentation scope (Meter):",
                $"\tName: {meterName}",
                "\tVersion: 1.2.3",
                "\tSchema URL: https://opentelemetry.io/schemas/1.40.0",
                "\tTags:",
            ],
            scope.Take(5));

        // The order the tags of a Meter are enumerated in is not defined
        Assert.Equal(
            [
                "\t\tmeter.number: 42",
                "\t\tmeter.string: value",
            ],
            scope.Skip(5).OrderBy((p) => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Export_ExponentialHistogramWithZeroValue_WritesZeroBucket()
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

        histogram.Record(0);
        histogram.Record(0);

        // Whole numbers are used as the exporter formats the sum using the current culture on .NET Framework
        histogram.Record(10);
        meterProvider!.ForceFlush();

        Assert.Single(metrics);

        // Act
        using var capture = new TraceOutputCapture();
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions { Targets = ConsoleExporterOutputTargets.Debug });
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);

        var point = Assert.Single(capture.Lines, (p) => p.Contains("Value: Sum:", StringComparison.Ordinal));
        Assert.Contains("Sum: 10 Count: 3 ", point, StringComparison.Ordinal);
        Assert.Contains($"Zero Bucket:2{Environment.NewLine}", point, StringComparison.Ordinal);
        Assert.Contains("]:1", point, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Export_WithExemplars_WritesExemplars(bool useDouble, bool withTraceContext)
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);

        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView("test-counter", new MetricStreamConfiguration { TagKeys = ["included"] })
            .AddInMemoryExporter(metrics)
            .Build();

        using var activity = withTraceContext ? new Activity("test") : null;
        activity?.Start();

        var includedTag = new KeyValuePair<string, object?>("included", "a");
        var excludedTag = new KeyValuePair<string, object?>("excluded", "b");

        if (useDouble)
        {
            meter.CreateCounter<double>("test-counter").Add(5, includedTag, excludedTag);
        }
        else
        {
            meter.CreateCounter<long>("test-counter").Add(5, includedTag, excludedTag);
        }

        meterProvider!.ForceFlush();

        Assert.Single(metrics);

        // Act
        using var capture = new TraceOutputCapture();
        using var exporter = new ConsoleMetricExporter(new ConsoleExporterOptions { Targets = ConsoleExporterOutputTargets.Debug });
        var actual = exporter.Export(new Batch<Metric>([.. metrics], metrics.Count));

        // Assert
        Assert.Equal(ExportResult.Success, actual);

        var point = Assert.Single(capture.Lines, (p) => p.Contains("Value: 5", StringComparison.Ordinal));
        Assert.Contains("included: a", point, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}Exemplars{Environment.NewLine}Timestamp: ", point, StringComparison.Ordinal);

        var exemplar = point.Substring(point.IndexOf("Exemplars", StringComparison.Ordinal));
        Assert.Contains(" Value: 5", exemplar, StringComparison.Ordinal);
        Assert.Contains(" Filtered Tags: excluded: b ", exemplar, StringComparison.Ordinal);

        if (activity is not null)
        {
            Assert.Contains($" TraceId: {activity.TraceId.ToHexString()} SpanId: {activity.SpanId.ToHexString()}", exemplar, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("TraceId", exemplar, StringComparison.Ordinal);
        }
    }
}
