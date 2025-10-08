// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public static class ProtobufOtlpMetricSerializerTests
{
    [Fact]
    public static async Task WriteMetricsData_Serializes_Metrics_Correctly()
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

            using var meterProvider = builder.Build();
            using var meter = new Meter(meterName);

            var counter = meter.CreateCounter<int>("counter");
            counter.Add(18);

            var gauge = meter.CreateGauge<int>("gauge");
            gauge.Record(42);

            var histogram = meter.CreateHistogram<int>("histogram");
            histogram.Record(100);

            Assert.True(meterProvider.ForceFlush());

            Assert.NotEqual(0, metrics.Count);
        }

        // Scrub the timestamps for stable snapshots
        var startTime = new DateTimeOffset(2025, 10, 08, 10, 20, 11, TimeSpan.Zero);
        var endTime = startTime.AddSeconds(10);

        foreach (var metric in metrics)
        {
            metric.AggregatorStore.OverrideTimeRange(startTime, endTime);
        }

        var attributes = new Dictionary<string, object>
        {
            { "service.name", "OpenTelemetry-DotNet" },
            { "service.version", "1.2.3" },
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
}
