// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public static class ProtobufOtlpMetricSerializerTests
{
    private const string HistogramName = "histogram";

    [Theory]
    [InlineData(700)]
    [InlineData(2000)]
    public static void WriteMetricsData_Serializes_Metrics_With_OversizedMetadata(int descriptionLength)
    {
        var description = new string('a', descriptionLength);
        var metrics = GenerateMetricWithDescription(description);

        var buffer = new byte[16 * 1024];
        var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref buffer,
            0,
            Resource.Empty,
            metrics);

        Assert.True(writePosition > 0);
        Assert.True(writePosition <= buffer.Length);

        using var stream = new MemoryStream(buffer, 0, writePosition);
        var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
        var parsedMetric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];
        Assert.Equal(description, parsedMetric.Description);
    }

    [Fact]
    public static void WriteMetricsDataDoesNotKeepMetricAlive()
    {
        var reference = CreateSerializedMetricWeakReference();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.TryGetTarget(out _), "Metric should not be kept alive after serialization.");
    }

    [Fact]
    public static async Task WriteMetricsData_Serializes_Metrics_Correctly()
    {
        // Arrange
        var metrics = GenerateMetrics();

        // Act and Assert
        await WriteMetricsAndAssertSnapshot(metrics);
    }

    [Fact]
    public static async Task WriteMetricsData_Serializes_Metrics_With_Explicit_Boundaries()
    {
        // Arrange
        var metrics = GenerateMetrics((builder) =>
        {
            builder.AddView(
                instrumentName: HistogramName,
                new ExplicitBucketHistogramConfiguration { Boundaries = [1, 2, 4, 8, 16] });
        });

        // Act and Assert
        await WriteMetricsAndAssertSnapshot(metrics);
    }

    [Fact]
    public static async Task WriteMetricsData_Serializes_Metrics_With_No_Boundaries()
    {
        // Arrange
        var metrics = GenerateMetrics((builder) =>
        {
            builder.AddView(
                instrumentName: HistogramName,
                new ExplicitBucketHistogramConfiguration { Boundaries = [] });
        });

        // Act and Assert
        await WriteMetricsAndAssertSnapshot(metrics);
    }

    [Fact]
    public static void WriteMetricsData_BatchTooLargeForMaxBufferSize_Throws()
    {
        var metrics = GenerateHighCardinalityMetrics(cardinality: 20_000);

        var buffer = ProtobufSerializer.RentBuffer(1024);
        try
        {
            // 1 MiB cannot hold 20,000 metric points, and the serializer is not
            // allowed to grow past it, so the batch cannot be serialized.
            Assert.ThrowsAny<Exception>(() => ProtobufOtlpMetricSerializer.WriteMetricsData(
                ref buffer,
                0,
                Resource.Empty,
                metrics,
                maxBufferSize: 1024 * 1024));
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }

    [Fact]
    public static void WriteMetricsData_LargerMaxBufferSizeAllowsLargerBatch()
    {
        var metrics = GenerateHighCardinalityMetrics(cardinality: 20_000);

        var buffer = ProtobufSerializer.RentBuffer(1024);
        try
        {
            // The same batch that does not fit within 1 MiB is serialized when
            // the serializer is allowed to grow further.
            var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(
                ref buffer,
                0,
                Resource.Empty,
                metrics,
                maxBufferSize: 16 * 1024 * 1024);

            Assert.True(writePosition > 1024 * 1024, $"Expected a payload larger than 1 MiB but was {writePosition}.");

            using var stream = new MemoryStream(buffer, 0, writePosition);
            var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);

            var resourceMetrics = Assert.Single(request.ResourceMetrics);
            var scopeMetrics = Assert.Single(resourceMetrics.ScopeMetrics);
            var metric = Assert.Single(scopeMetrics.Metrics);

            Assert.Equal(20_000, metric.Sum.DataPoints.Count);
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }

    [Theory]
    [InlineData(2 * 1024 * 1024)]
    [InlineData(3 * 1024 * 1024)] // Not a power of two, so not a size doubling reaches exactly.
    [InlineData(ProtobufSerializer.DefaultMaxBufferSize)]
    [InlineData(ProtobufSerializer.AbsoluteMaxBufferSize)]
    public static void IncreaseBufferSize_GrowsToMaxBufferSizeThenStops(int maxBufferSize)
    {
        var buffer = ProtobufSerializer.RentBuffer(ProtobufSerializer.InitialBufferSize);
        try
        {
            var growths = 0;

            while (ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics, maxBufferSize))
            {
                Assert.True(++growths < 64, "Growth did not terminate.");
            }

            // The whole budget is usable: growth only stops once the buffer has
            // reached the maximum, leaving no unreachable remainder.
            Assert.True(
                buffer.Length >= maxBufferSize,
                $"Buffer stopped growing at {buffer.Length}, short of the maximum of {maxBufferSize}.");

            // How much the array pool hands back over what was asked for is up to
            // the runtime, but it is never more than a further doubling.
            Assert.True(
                buffer.Length <= 2L * maxBufferSize,
                $"Buffer grew to {buffer.Length}, more than double the maximum of {maxBufferSize}.");

            // Growth stays refused once the maximum has been reached.
            Assert.False(ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics, maxBufferSize));
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }

    private static async Task WriteMetricsAndAssertSnapshot(Batch<Metric> metrics)
    {
        // Arrange
        var attributes = new Dictionary<string, object>
        {
            ["service.name"] = "OpenTelemetry-DotNet",
            ["service.version"] = "1.2.3",
        };

        var buffer = new byte[1024];
        var writePosition = 0;
        var resource = new Resource(attributes);

        // Act
        var actual = ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref buffer,
            writePosition,
            resource,
            metrics);

        // Assert
        Assert.NotEqual(0, actual);
        Assert.True(actual > writePosition, $"The returned write position, {actual} is not greater than the initial write position, {writePosition}.");
        Assert.True(actual <= buffer.Length, $"The returned write position, {actual} is beyond the bounds of the buffer, {buffer.Length}.");

        using var stream = new MemoryStream();

#if NET
        await stream.WriteAsync(buffer.AsMemory(0, actual));
#else
        await stream.WriteAsync(buffer, 0, actual);
#endif

        await Verify(stream, "bin")
            .IgnoreParametersForVerified()
            .UseDirectory("snapshots");
    }

    private static Batch<Metric> GenerateMetrics(Action<MeterProviderBuilder>? configure = null)
    {
        // Arrange
        Batch<Metric> metrics = default;

        // Create some metrics to export
        using (var exported = new ManualResetEvent(false))
        {
            var experimentalOptions = new ExperimentalOptions();
            var exporterOptions = new OtlpExporterOptions()
            {
                Endpoint = new($"http://localhost:4318/v1/"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
            };

            using var exporter = new DelegatingExporter<Metric>()
            {
                OnExportFunc = (batch) =>
                {
                    metrics = batch;
                    exported.Set();
                    return ExportResult.Success;
                },
            };

            var meterName = "otlp.protobuf.serialization";

            var builder = Sdk.CreateMeterProviderBuilder().AddMeter(meterName);

            var metricReaderOptions = new MetricReaderOptions();
            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite;

            builder.AddReader(
                (serviceProvider) => OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                    serviceProvider,
                    exporterOptions,
                    metricReaderOptions,
                    experimentalOptions,
                    configureExporterInstance: (_) => exporter));

            configure?.Invoke(builder);

            using var meterProvider = builder.Build();
            using var meter = new Meter(meterName);

            var counter = meter.CreateCounter<int>("counter");
            counter.Add(18);

            var gauge = meter.CreateGauge<int>("gauge");
            gauge.Record(42);

            var histogram = meter.CreateHistogram<int>(HistogramName);
            histogram.Record(100);

            Assert.True(meterProvider.ForceFlush());

            Assert.NotEqual(0, metrics.Count);
        }

        // Scrub the timestamps for stable snapshots
        var startTime = new DateTimeOffset(2025, 10, 08, 10, 20, 11, TimeSpan.Zero);
        var endTime = startTime.AddSeconds(10);

        var type = typeof(AggregatorStore);
        var bindingAttributes = BindingFlags.NonPublic | BindingFlags.Instance;

        var startTimeProperty = type.GetProperty(nameof(AggregatorStore.StartTimeExclusive), bindingAttributes);
        var endTimeProperty = type.GetProperty(nameof(AggregatorStore.EndTimeInclusive), bindingAttributes);

        foreach (var metric in metrics)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            startTimeProperty.SetValue(metric.AggregatorStore, startTime);
            endTimeProperty.SetValue(metric.AggregatorStore, endTime);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        return metrics;
    }

    private static Batch<Metric> GenerateMetricWithDescription(string description)
    {
        Batch<Metric> metrics = default;

        using (var exported = new ManualResetEvent(false))
        {
            using var exporter = new DelegatingExporter<Metric>()
            {
                OnExportFunc = (batch) =>
                {
                    metrics = batch;
                    exported.Set();
                    return ExportResult.Success;
                },
            };

            var meterName = "otlp.protobuf.large-metadata";

            var experimentalOptions = new ExperimentalOptions();
            var exporterOptions = new OtlpExporterOptions()
            {
                Endpoint = new($"http://localhost:4318/v1/"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
            };

            var metricReaderOptions = new MetricReaderOptions();
            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite;

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterName)
                .AddReader(
                    (serviceProvider) => OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        serviceProvider,
                        exporterOptions,
                        metricReaderOptions,
                        experimentalOptions,
                        configureExporterInstance: (_) => exporter))
                .Build();

            using var meter = new Meter(meterName);

            var counter = meter.CreateCounter<long>(name: "test.counter", unit: "1", description: description);
            counter.Add(1);

            Assert.True(meterProvider.ForceFlush());
            Assert.NotEqual(0, metrics.Count);
        }

        return metrics;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Metric> CreateSerializedMetricWeakReference()
    {
        var metrics = GenerateMetrics();

        Metric capturedMetric = null!;
        foreach (var metric in metrics)
        {
            capturedMetric = metric;
            break;
        }

        var buffer = ProtobufSerializer.RentBuffer(16 * 1024);
        try
        {
            _ = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics);
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }

        return new WeakReference<Metric>(capturedMetric);
    }

    private static Batch<Metric> GenerateHighCardinalityMetrics(int cardinality)
    {
        var exported = new List<Metric>();
        var meterName = Utils.GetCurrentMethodName() + Guid.NewGuid().ToString("N");

        int count;

        using (var meter = new Meter(meterName))
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddMeter(meterName)
                                      .AddView("*", new MetricStreamConfiguration { CardinalityLimit = cardinality + 10 })
                                      .AddInMemoryExporter(exported)
                                      .Build())
        {
            var counter = meter.CreateCounter<long>("test.counter");
            for (var i = 0; i < cardinality; i++)
            {
                counter.Add(1, new KeyValuePair<string, object?>("tag", $"value-{i}"));
            }

            Assert.True(meterProvider.ForceFlush());

            count = exported.Count;
        }

        Assert.Equal(1, count);

        return new Batch<Metric>([.. exported], count);
    }
}
