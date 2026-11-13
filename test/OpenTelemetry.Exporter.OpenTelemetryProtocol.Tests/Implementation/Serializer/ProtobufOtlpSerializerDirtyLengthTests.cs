// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

/// <summary>
/// Each test drops a final key-value list attribute after a long entry has been written,
/// so the rolled-back bytes reach past the end of the serialized payload.
/// </summary>
public class ProtobufOtlpSerializerDirtyLengthTests
{
    [Fact]
    public void WriteTraceDataReportsBytesWrittenPastTheSerializedLength()
    {
        var sourceName = $"{nameof(ProtobufOtlpSerializerDirtyLengthTests)}.{nameof(this.WriteTraceDataReportsBytesWrittenPastTheSerializedLength)}";
        using var source = new ActivitySource(new ActivitySourceOptions(sourceName));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("activity");
        Assert.NotNull(activity);
        activity.SetTag("key", ThrowAfterLongEntry());

        var buffer = new byte[8 * 1024];
        var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(
            ref buffer,
            0,
            new SdkLimitOptions(),
            Resource.Empty,
            new Batch<Activity>([activity], 1),
            out var dirtyLength);

        Assert.True(dirtyLength > writePosition, $"{dirtyLength} <= {writePosition}");
    }

    [Fact]
    public void WriteLogsDataReportsBytesWrittenPastTheSerializedLength()
    {
        var logRecords = new List<LogRecord>();
        using (var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(options => options.AddInMemoryExporter(logRecords))))
        {
            var state = new List<KeyValuePair<string, object?>> { new("key", ThrowAfterLongEntry()) };
            loggerFactory.CreateLogger("logger").Log(LogLevel.Information, default, state, null, (_, _) => "message");
        }

        var buffer = new byte[8 * 1024];
        var writePosition = ProtobufOtlpLogSerializer.WriteLogsData(
            ref buffer,
            0,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            Resource.Empty,
            new Batch<LogRecord>([Assert.Single(logRecords)], 1),
            out var dirtyLength);

        Assert.True(dirtyLength > writePosition, $"{dirtyLength} <= {writePosition}");
    }

    [Fact]
    public void WriteMetricsDataReportsBytesWrittenPastTheSerializedLength()
    {
        var meterName = $"{nameof(ProtobufOtlpSerializerDirtyLengthTests)}.{nameof(this.WriteMetricsDataReportsBytesWrittenPastTheSerializedLength)}";
        var exportedMetrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var meter = new Meter(new MeterOptions(meterName) { Tags = [new("key", ThrowAfterLongEntry())] });
        meter.CreateCounter<long>("counter").Add(1);

        Assert.True(meterProvider.ForceFlush());

        var buffer = new byte[8 * 1024];
        var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(
            ref buffer,
            0,
            Resource.Empty,
            new Batch<Metric>([Assert.Single(exportedMetrics)], 1),
            out var dirtyLength);

        Assert.True(dirtyLength > writePosition, $"{dirtyLength} <= {writePosition}");
    }

    private static IEnumerable<KeyValuePair<string, object?>> ThrowAfterLongEntry()
    {
        yield return new("inner", new string('v', 1024));
        throw new InvalidOperationException("Nope.");
    }
}
