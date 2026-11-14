// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public static class ProtobufOtlpMetricSerializerTests
{
    private const string HistogramName = "histogram";

#if NET
    [Fact]
    public static void WriteMetricsData_WithWarmedCacheDoesNotAllocate()
    {
        var metrics = GenerateMetricWithDescription("Cached metadata");
        var buffer = new byte[16 * 1024];

        // Warm metadata caches and serializer pools before measuring repeated exports.
        for (var i = 0; i < 10000; i++)
        {
            ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);
        }

        var writePosition = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);

        using var stream = new MemoryStream(buffer, 0, writePosition);
        var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
        var parsedMetric = Assert.Single(Assert.Single(Assert.Single(request.ResourceMetrics).ScopeMetrics).Metrics);
        Assert.Equal("Cached metadata", parsedMetric.Description);
    }
#endif

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
            metrics,
            out _);

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
    public static void WriteMetricsData_RejectsMetadataLargerThanMaximumBufferSize()
    {
        const int maxBufferSize = 1024;

        var metrics = GenerateMetricWithDescription(new string('a', maxBufferSize));

        // Model ArrayPool returning more capacity than the configured maximum.
        var buffer = new byte[maxBufferSize * 2];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref buffer,
            0,
            Resource.Empty,
            metrics,
            out _,
            maxBufferSize));

        Assert.Equal("size", exception.ParamName);
        var actualValue = Assert.IsType<long>(exception.ActualValue);
        Assert.True(actualValue > maxBufferSize, $"Metadata size {actualValue} did not exceed {maxBufferSize}.");
    }

    [Fact]
    public static void WriteMetricsData_RejectsCachedMetadataLargerThanMaximumBufferSize()
    {
        const int maxBufferSize = 1024;

        var metrics = GenerateMetricWithDescription(new string('a', maxBufferSize));
        var initialBuffer = new byte[maxBufferSize * 4];

        var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref initialBuffer,
            0,
            Resource.Empty,
            metrics,
            out _,
            initialBuffer.Length);
        Assert.True(writePosition > 0);

        // Model ArrayPool returning more capacity than the configured maximum.
        var limitedBuffer = new byte[maxBufferSize * 2];
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref limitedBuffer,
            0,
            Resource.Empty,
            metrics,
            out _,
            maxBufferSize));

        Assert.Equal("cachedMetadata", exception.ParamName);
        var actualValue = Assert.IsType<long>(exception.ActualValue);
        Assert.True(actualValue > maxBufferSize, $"Metadata size {actualValue} did not exceed {maxBufferSize}.");
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
    public static async Task WriteMetricsData_Serializes_Metrics_With_Infinite_Boundaries()
    {
        var metrics = GenerateMetrics((builder) =>
        {
            builder.AddView(
                instrumentName: HistogramName,
                new ExplicitBucketHistogramConfiguration { Boundaries = [double.NegativeInfinity, 1, 2, double.PositiveInfinity] });
        });

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
    public static void IncreaseBufferSize_GrowsToMaxBufferSizeThenStops()
    {
        var buffer = ProtobufSerializer.RentBuffer(ProtobufSerializer.InitialBufferSize);
        var maxBufferSize = ProtobufSerializer.MaxBufferSize;

        try
        {
            var growths = 0;

            while (ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics))
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
            Assert.False(ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics));
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }

    [Fact]
    public static void WriteMetricsData_Reuses_Cached_DataPoint_Attributes()
    {
        var metrics = GenerateMetricsWithTags();

        var first = new byte[64 * 1024];
        var firstLength = ProtobufOtlpMetricSerializer.WriteMetricsData(ref first, 0, Resource.Empty, metrics, out _);

        var second = new byte[64 * 1024];
        var secondLength = ProtobufOtlpMetricSerializer.WriteMetricsData(ref second, 0, Resource.Empty, metrics, out _);

        Assert.True(firstLength > 0);
        Assert.Equal(firstLength, secondLength);
        Assert.Equal(first.AsSpan(0, firstLength).ToArray(), second.AsSpan(0, secondLength).ToArray());

        using var stream = new MemoryStream(second, 0, secondLength);
        var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
        var dataPoints = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0].Sum.DataPoints;

        Assert.Equal(2, dataPoints.Count);
        Assert.All(dataPoints, static point => Assert.Equal(2, point.Attributes.Count));
    }

    [Fact]
    public static void WriteMetricsData_Serializes_Distinct_Attributes_Per_DataPoint()
    {
        var metrics = GenerateMetricsWithTags();

        var buffer = new byte[64 * 1024];
        var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);

        using var stream = new MemoryStream(buffer, 0, length);
        var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
        var dataPoints = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0].Sum.DataPoints;

        var routes = dataPoints
            .Select(static point => point.Attributes.Single(static a => a.Key == "route").Value.StringValue)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["/a", "/b"], routes);
    }

    [Fact]
    public static void WriteMetricsData_Keeps_ToString_Attributes_When_Buffer_Is_Resized()
    {
        const int DataPointCount = 32;

        var metrics = GenerateMetricsWithTags(static counter =>
        {
            for (var i = 0; i < DataPointCount; i++)
            {
                counter.Add(
                    1,
                    new KeyValuePair<string, object?>("route", "/a"),
                    new KeyValuePair<string, object?>("url", new Uri($"http://localhost/{i}/{new string('a', 256)}")));
            }
        });

        // The buffer has to grow part way through the data points, and the
        // second export replays whatever the first one cached.
        for (var export = 0; export < 2; export++)
        {
            var buffer = ProtobufSerializer.RentBuffer(1024);

            try
            {
                var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);

                using var stream = new MemoryStream(buffer, 0, length);
                var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
                var dataPoints = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0].Sum.DataPoints;

                Assert.Equal(DataPointCount, dataPoints.Count);
                Assert.All(dataPoints, static point => Assert.Contains(point.Attributes, static a => a.Key == "url"));
            }
            finally
            {
                ProtobufSerializer.ReturnBuffer(buffer);
            }
        }
    }

    [Fact]
    public static void WriteMetricsData_Does_Not_Cache_DataPoint_Attributes_When_A_Tag_Is_Dropped()
    {
        var metrics = GenerateMetricsWithTags(static counter => counter.Add(
            1,
            new KeyValuePair<string, object?>("route", "/a"),
            new KeyValuePair<string, object?>("transient", new ToStringThrowsOnce())));

        var buffer = new byte[64 * 1024];

        Assert.Equal(["route"], SerializeAndGetAttributeKeys());
        Assert.Equal(["route", "transient"], SerializeAndGetAttributeKeys());

        string[] SerializeAndGetAttributeKeys()
        {
            var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);

            using var stream = new MemoryStream(buffer, 0, length);
            var request = OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
            var dataPoint = Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0].Sum.DataPoints);

            return [.. dataPoint.Attributes.Select(static a => a.Key)];
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(16)]
    public static void WriteMetricsData_BatchLargerThanRequest_IsWrittenAsSeveralRequestsCutAtMetricPoints(int divisor)
    {
        const string ResourceSchemaUrl = "https://opentelemetry.io/schemas/resource";

        var resource = new Resource(new Dictionary<string, object> { ["service.name"] = "cut" }, ResourceSchemaUrl);
        var metrics = GenerateMetricsOfEveryType();

        var buffer = new byte[1024];
        var wholeLength = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, resource, metrics, out _);
        var expected = DescribeDataPoints(ParseRequest(buffer, wholeLength));

        // A request one byte short of the whole batch, or a fraction of it.
        var limit = divisor == 1 ? wholeLength - 1 : wholeLength / divisor;

        var actual = new List<string>();
        var cursor = default(ProtobufOtlpMetricSerializer.MetricsDataCursor);
        var requestCount = 0;
        bool completed;

        do
        {
            var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, resource, metrics, out var dirtyLength, limit, ref cursor, out completed);

            Assert.True(length <= limit, $"Request {requestCount} of {length} bytes exceeds the limit of {limit} bytes.");

            // The bytes written beyond where a request is cut have to be cleared before
            // the buffer goes back to the pool, so they count towards its dirty prefix.
            Assert.True(
                completed || dirtyLength > length,
                $"Request {requestCount} of {length} bytes reported a dirty length of {dirtyLength} bytes.");

            // Every request is a complete message, with the resource and the scopes
            // of its metrics, and holds no metric without metric points.
            var request = ParseRequest(buffer, length);
            var resourceMetrics = Assert.Single(request.ResourceMetrics);
            Assert.Equal(ResourceSchemaUrl, resourceMetrics.SchemaUrl);
            Assert.Equal("cut", Assert.Single(resourceMetrics.Resource.Attributes).Value.StringValue);
            Assert.NotEmpty(resourceMetrics.ScopeMetrics);

            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                Assert.Equal($"{scopeMetrics.Scope.Name}/schema", scopeMetrics.SchemaUrl);
                Assert.NotEmpty(scopeMetrics.Metrics);
            }

            var dataPoints = DescribeDataPoints(request);
            Assert.Equal(resourceMetrics.ScopeMetrics.Sum(static s => s.Metrics.Count), dataPoints.Select(static d => d.Split('|')[0]).Distinct().Count());

            actual.AddRange(dataPoints);
            requestCount++;
        }
        while (!completed);

        Assert.True(requestCount > 1, "The batch was expected to need more than one request.");
        Assert.Equal(0, cursor.DroppedMetricPointCount);
        Assert.Equal(expected.OrderBy(static d => d, StringComparer.Ordinal), actual.OrderBy(static d => d, StringComparer.Ordinal));
    }

    [Fact]
    public static void WriteMetricsData_MetricPointLargerThanRequest_IsDroppedAndTheRestWritten()
    {
        const int MetricPointDroppedEventId = 45;

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        var metrics = GenerateMetricsWithTags(static counter =>
        {
            counter.Add(1, new KeyValuePair<string, object?>("size", "small-1"));
            counter.Add(2, new KeyValuePair<string, object?>("size", new string('x', 4096)));
            counter.Add(3, new KeyValuePair<string, object?>("size", "small-3"));
        });

        var buffer = new byte[256];
        var cursor = default(ProtobufOtlpMetricSerializer.MetricsDataCursor);
        var values = new List<long>();
        bool completed;

        do
        {
            var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _, 1024, ref cursor, out completed);

            Assert.True(length <= 1024, $"The request of {length} bytes exceeds the limit.");

            var request = ParseRequest(buffer, length);
            var metric = Assert.Single(Assert.Single(Assert.Single(request.ResourceMetrics).ScopeMetrics).Metrics);
            values.AddRange(metric.Sum.DataPoints.Select(static p => p.AsInt));
        }
        while (!completed);

        Assert.Equal([1, 3], values);
        Assert.Equal(1, cursor.DroppedMetricPointCount);

        var dropped = Assert.Single(listener.Messages, static e => e.EventId == MetricPointDroppedEventId);
        Assert.Equal("requests", dropped.Payload?[0]);
        Assert.Equal(1024, dropped.Payload?[1]);
    }

    [Fact]
    public static void WriteMetricsData_LastMetricPointLargerThanRequest_IsDroppedWithoutAnEmptyRequest()
    {
        var metrics = GenerateMetricsWithTags(static counter =>
        {
            counter.Add(1, new KeyValuePair<string, object?>("size", "small-1"));
            counter.Add(2, new KeyValuePair<string, object?>("size", new string('x', 4096)));
        });

        var buffer = new byte[1024];
        var cursor = default(ProtobufOtlpMetricSerializer.MetricsDataCursor);

        // The first request is cut after the first metric point.
        var length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _, 1024, ref cursor, out var completed);

        Assert.False(completed);
        var metric = Assert.Single(Assert.Single(Assert.Single(ParseRequest(buffer, length).ResourceMetrics).ScopeMetrics).Metrics);
        Assert.Equal([1], metric.Sum.DataPoints.Select(static p => p.AsInt));

        // The second metric point is dropped, after which nothing is left to send, so
        // no request holding only the resource, scope and metric is produced.
        length = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out var dirtyLength, 1024, ref cursor, out completed);

        Assert.True(completed);
        Assert.Equal(0, length);
        Assert.True(dirtyLength > 0, "The bytes written before the request was abandoned have to be cleared.");
        Assert.Equal(1, cursor.DroppedMetricPointCount);
    }

    [Fact]
    public static void WriteMetricsData_RequestTooSmallForAnyMetricPoint_Throws()
    {
        var metrics = GenerateMetricsWithTags();
        var buffer = new byte[16];
        var cursor = default(ProtobufOtlpMetricSerializer.MetricsDataCursor);

        // Not even the resource, scope and metric a metric point belongs to fit.
        Assert.ThrowsAny<Exception>(() => ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _, 16, ref cursor, out _));

        Assert.Equal(default, cursor.MetricIndex);
        Assert.Equal(default, cursor.MetricPointIndex);
    }

    private static OtlpCollector.ExportMetricsServiceRequest ParseRequest(byte[] buffer, int length)
    {
        using var stream = new MemoryStream(buffer, 0, length);
        return OtlpCollector.ExportMetricsServiceRequest.Parser.ParseFrom(stream);
    }

    /// <summary>
    /// Describes every data point of a request as a string, so that the data
    /// points of several requests can be compared with those of a single one.
    /// </summary>
    private static List<string> DescribeDataPoints(OtlpCollector.ExportMetricsServiceRequest request)
    {
        var result = new List<string>();

        foreach (var resourceMetrics in request.ResourceMetrics)
        {
            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                foreach (var metric in scopeMetrics.Metrics)
                {
                    var prefix = $"{scopeMetrics.Scope.Name}/{metric.Name}";

                    switch (metric.DataCase)
                    {
                        case OtlpMetrics.Metric.DataOneofCase.Sum:
                            result.AddRange(metric.Sum.DataPoints.Select(p => $"{prefix}|{Describe(p.Attributes)}|{p.AsInt}|{p.AsDouble}|{p.Exemplars.Count}"));
                            break;

                        case OtlpMetrics.Metric.DataOneofCase.Gauge:
                            result.AddRange(metric.Gauge.DataPoints.Select(p => $"{prefix}|{Describe(p.Attributes)}|{p.AsInt}|{p.AsDouble}|{p.Exemplars.Count}"));
                            break;

                        case OtlpMetrics.Metric.DataOneofCase.Histogram:
                            result.AddRange(metric.Histogram.DataPoints.Select(p => $"{prefix}|{Describe(p.Attributes)}|{p.Count}|{p.Sum}|{p.BucketCounts.Count}|{p.ExplicitBounds.Count}|{p.Exemplars.Count}"));
                            break;

                        case OtlpMetrics.Metric.DataOneofCase.ExponentialHistogram:
                            result.AddRange(metric.ExponentialHistogram.DataPoints.Select(p => $"{prefix}|{Describe(p.Attributes)}|{p.Count}|{p.Sum}|{p.Scale}|{p.Positive.BucketCounts.Count}|{p.Exemplars.Count}"));
                            break;

                        default:
                            Assert.Fail($"Unexpected metric data '{metric.DataCase}'.");
                            break;
                    }
                }
            }
        }

        return result;

        static string Describe(IEnumerable<OtlpCommon.KeyValue> attributes)
            => string.Join(",", attributes.Select(static a => $"{a.Key}={a.Value}"));
    }

    /// <summary>
    /// Generates metrics of every type from two scopes with schema URLs, each with
    /// several metric points carrying exemplars.
    /// </summary>
    private static Batch<Metric> GenerateMetricsOfEveryType()
    {
        const int MetricPointsPerMetric = 12;

        var exported = new List<Metric>();
        var meterNames = new[]
        {
            $"{nameof(ProtobufOtlpMetricSerializerTests)}.{nameof(GenerateMetricsOfEveryType)}.1",
            $"{nameof(ProtobufOtlpMetricSerializerTests)}.{nameof(GenerateMetricsOfEveryType)}.2",
        };

        var meters = new List<Meter>();
        int count;

        try
        {
            using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterNames)
                .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
                .AddView("exponential.histogram", new Base2ExponentialBucketHistogramConfiguration())
                .AddInMemoryExporter(exported)
                .Build())
            {
                foreach (var meterName in meterNames)
                {
                    var meter = new Meter(new MeterOptions(meterName) { TelemetrySchemaUrl = $"{meterName}/schema" });
                    meters.Add(meter);

                    var longCounter = meter.CreateCounter<long>("long.counter");
                    var doubleCounter = meter.CreateCounter<double>("double.counter");
                    var histogram = meter.CreateHistogram<double>("histogram");
                    var exponentialHistogram = meter.CreateHistogram<double>("exponential.histogram");
                    var gaugeValues = new List<Measurement<long>>();
                    meter.CreateObservableGauge("gauge", () => gaugeValues);

                    for (var i = 0; i < MetricPointsPerMetric; i++)
                    {
                        var tag = new KeyValuePair<string, object?>("index", i);

                        longCounter.Add(i, tag);
                        doubleCounter.Add(i + 0.5, tag);
                        histogram.Record(i * 10, tag);
                        exponentialHistogram.Record(i + 1, tag);
                        gaugeValues.Add(new Measurement<long>(i, tag));
                    }
                }

                Assert.True(meterProvider.ForceFlush());

                // Disposing the provider collects and exports the same metrics again.
                count = exported.Count;
            }
        }
        finally
        {
            foreach (var meter in meters)
            {
                meter.Dispose();
            }
        }

        Assert.Equal(10, count);

        return new Batch<Metric>([.. exported], count);
    }

    private static Batch<Metric> GenerateMetricsWithTags(Action<Counter<long>>? record = null)
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

            var meterName = Utils.GetCurrentMethodName() + Guid.NewGuid().ToString("N");
            using var meter = new Meter(meterName);

#pragma warning disable CA2000 // Ownership is transferred to the MeterProvider via AddReader.
            var reader = new BaseExportingMetricReader(exporter);
#pragma warning restore CA2000

            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterName)
                .AddReader(reader)
                .Build();

            var counter = meter.CreateCounter<long>("requests");

            if (record != null)
            {
                record(counter);
            }
            else
            {
                counter.Add(
                    1,
                    new KeyValuePair<string, object?>("route", "/a"),
                    new KeyValuePair<string, object?>("status", 200));

                counter.Add(
                    2,
                    new KeyValuePair<string, object?>("route", "/b"),
                    new KeyValuePair<string, object?>("status", 500));
            }

            provider.ForceFlush();
            exported.WaitOne(TimeSpan.FromSeconds(30));
        }

        return metrics;
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
            metrics,
            out _);

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
            _ = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, Resource.Empty, metrics, out _);
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }

        return new WeakReference<Metric>(capturedMetric);
    }

    private sealed class ToStringThrowsOnce
    {
        private bool thrown;

        public override string ToString()
        {
            if (!this.thrown)
            {
                this.thrown = true;
                throw new InvalidOperationException("Nope.");
            }

            return "value";
        }
    }
}
