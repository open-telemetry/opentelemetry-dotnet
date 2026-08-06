// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CA2000 // Provider takes ownership of processor/exporter

using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Logs;

public class BatchLogRecordExportProcessorSelfObservabilityTests
{
    [Fact]
    public async Task BatchProcessor_CountsSuccessWhenSubmittedToExporter()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var exportStarted = new ManualResetEventSlim(false);
        using var allowExport = new ManualResetEventSlim(false);
        using var exporter = new DelegatingExporter<LogRecord>
        {
            OnExportFunc = batch =>
            {
                exportStarted.Set();
                allowExport.Wait();
                return ExportResult.Success;
            },
        };
        using var processor = new BatchLogRecordExportProcessor(
            exporter,
            scheduledDelayMilliseconds: int.MaxValue);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        var logger = loggerProvider.GetLogger("test");
        logger.EmitLog(new LogRecordData());
        logger.EmitLog(new LogRecordData());
        logger.EmitLog(new LogRecordData());

        meterProvider.ForceFlush();
        Assert.DoesNotContain(exportedMetrics, m => m.Name == "otel.sdk.processor.log.processed");

        var flushTask = Task.Run(() => processor.ForceFlush());
        try
        {
            Assert.True(exportStarted.Wait(TimeSpan.FromSeconds(5)));
            meterProvider.ForceFlush();

            var metric = exportedMetrics.Single(m => m.Name == "otel.sdk.processor.log.processed");
            var points = GetMetricPoints(metric);
            var successPoint = points.Single(p => !HasTag(p, "error.type"));

            Assert.Equal(3, successPoint.GetSumLong());
            AssertTag(successPoint, "otel.component.type", "batching_log_processor");
            AssertTagStartsWith(successPoint, "otel.component.name", "batching_log_processor/");
        }
        finally
        {
            allowExport.Set();
        }

        Assert.True(await flushTask);
    }

    [Fact]
    public void BatchProcessor_QueueFull()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        // Use a blocking exporter so the worker thread holds the queue slot
        // while we overflow it, guaranteeing drops.
        using var exportStarted = new ManualResetEventSlim(false);
        using var allowExport = new ManualResetEventSlim(false);
        using var exporter = new DelegatingExporter<LogRecord>
        {
            OnExportFunc = batch =>
            {
                exportStarted.Set();
                allowExport.Wait();
                return ExportResult.Success;
            },
        };
        using var processor = new BatchLogRecordExportProcessor(
            exporter,
            maxQueueSize: 1,
            scheduledDelayMilliseconds: 1,
            maxExportBatchSize: 1);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        var logger = loggerProvider.GetLogger("test");

        // First log triggers the worker; wait for it to block in Export
        logger.EmitLog(new LogRecordData());
        exportStarted.Wait();

        // Now the queue is being drained but the worker is blocked.
        // Subsequent logs will overflow the queue (size=1).
        for (int i = 0; i < 5; i++)
        {
            logger.EmitLog(new LogRecordData());
        }

        // Release the exporter so shutdown can complete
        allowExport.Set();
        processor.ForceFlush();

        meterProvider.ForceFlush();

        var metric = exportedMetrics.Single(m => m.Name == "otel.sdk.processor.log.processed");
        var points = GetMetricPoints(metric);
        var queueFullPoint = points.Single(p => HasTagValue(p, "error.type", "queue_full"));

        Assert.True(queueFullPoint.GetSumLong() > 0);
        Assert.Equal(6, points.Sum(p => p.GetSumLong()));
    }

    [Fact]
    public void BatchProcessor_AfterShutdown()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var exportedLogs = new List<LogRecord>();
        using var exporter = new InMemoryExporter<LogRecord>(exportedLogs);
        var processor = new BatchLogRecordExportProcessor(exporter);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        var logger = loggerProvider.GetLogger("test");
        logger.EmitLog(new LogRecordData());
        logger.EmitLog(new LogRecordData());

        processor.Shutdown();
        logger.EmitLog(new LogRecordData());

        meterProvider.ForceFlush();

        var metric = exportedMetrics.Single(m => m.Name == "otel.sdk.processor.log.processed");
        var points = GetMetricPoints(metric);
        var shutdownPoint = points.Single(p => HasTagValue(p, "error.type", "already_shutdown"));
        var successPoint = points.Single(p => !HasTag(p, "error.type"));

        Assert.Equal(1, shutdownPoint.GetSumLong());
        Assert.Equal(2, successPoint.GetSumLong());
        Assert.Equal(2, exportedLogs.Count);
    }

    [Fact]
    public void MultipleProcessors_DistinctComponentNames()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var exporter1 = new InMemoryExporter<LogRecord>(new List<LogRecord>());
        using var exporter2 = new InMemoryExporter<LogRecord>(new List<LogRecord>());
        using var batch1 = new BatchLogRecordExportProcessor(exporter1);
        using var batch2 = new BatchLogRecordExportProcessor(exporter2);
        using var simple = new SimpleLogRecordExportProcessor(exporter1);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(batch1)
            .AddProcessor(batch2)
            .AddProcessor(simple)
            .Build();

        var logger = loggerProvider.GetLogger("test");
        logger.EmitLog(new LogRecordData());
        logger.EmitLog(new LogRecordData());

        batch1.ForceFlush();
        batch2.ForceFlush();
        meterProvider.ForceFlush();

        var metric = exportedMetrics.Single(m => m.Name == "otel.sdk.processor.log.processed");
        var points = GetMetricPoints(metric);

        // Each processor instance gets a unique otel.component.name, so we expect
        // separate MetricPoints for each: 2 batch + 1 simple = 3 distinct streams.
        var batchPoints = points.Where(p => HasTagValue(p, "otel.component.type", "batching_log_processor")).ToList();
        var simplePoints = points.Where(p => HasTagValue(p, "otel.component.type", "simple_log_processor")).ToList();

        Assert.Equal(2, batchPoints.Count);
        Assert.Single(simplePoints);

        // Each batch processor received the same 2 logs (composite processor fans out).
        Assert.All(batchPoints, p => Assert.Equal(2, p.GetSumLong()));
        Assert.Equal(2, simplePoints[0].GetSumLong());

        // Verify component names are distinct across batch processors.
        var batchNames = batchPoints
            .Select(p => GetTagValue(p, "otel.component.name"))
            .ToList();
        Assert.Equal(2, batchNames.Distinct().Count());
    }

    [Fact]
    public void SimpleProcessor_SuccessAndShutdown()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var exportedLogs = new List<LogRecord>();
        using var exporter = new InMemoryExporter<LogRecord>(exportedLogs);
        var processor = new SimpleLogRecordExportProcessor(exporter);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        var logger = loggerProvider.GetLogger("test");
        logger.EmitLog(new LogRecordData());
        logger.EmitLog(new LogRecordData());

        processor.Shutdown();
        logger.EmitLog(new LogRecordData());

        meterProvider.ForceFlush();

        var metric = exportedMetrics.Single(m => m.Name == "otel.sdk.processor.log.processed");
        var points = GetMetricPoints(metric);

        var successPoint = points.Single(p =>
            HasTagValue(p, "otel.component.type", "simple_log_processor") && !HasTag(p, "error.type"));
        var shutdownPoint = points.Single(p =>
            HasTagValue(p, "otel.component.type", "simple_log_processor") && HasTagValue(p, "error.type", "already_shutdown"));

        Assert.Equal(2, successPoint.GetSumLong());
        Assert.Equal(1, shutdownPoint.GetSumLong());
        Assert.Equal(2, exportedLogs.Count);
    }

    [Fact]
    public void NoListener_NoException()
    {
        // No MeterProvider subscribing to "otel.sdk.experimental" - verifying no crashes.
        using var exporter = new InMemoryExporter<LogRecord>(new List<LogRecord>());
        using var processor = new BatchLogRecordExportProcessor(exporter);
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        var logger = loggerProvider.GetLogger("test");
        for (int i = 0; i < 100; i++)
        {
            logger.EmitLog(new LogRecordData());
        }
    }

    private static List<MetricPoint> GetMetricPoints(Metric metric)
    {
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            points.Add(mp);
        }

        return points;
    }

    private static bool HasTag(MetricPoint point, string key)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTagValue(MetricPoint point, string key, string value)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == key && (string?)tag.Value == value)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetTagValue(MetricPoint point, string key)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == key)
            {
                return (string?)tag.Value;
            }
        }

        return null;
    }

    private static void AssertTag(MetricPoint point, string key, string expected)
    {
        var value = GetTagValue(point, key);
        Assert.NotNull(value);
        Assert.Equal(expected, value);
    }

    private static void AssertTagStartsWith(MetricPoint point, string key, string prefix)
    {
        var value = GetTagValue(point, key);
        Assert.NotNull(value);
        Assert.StartsWith(prefix, value, StringComparison.Ordinal);
    }
}
