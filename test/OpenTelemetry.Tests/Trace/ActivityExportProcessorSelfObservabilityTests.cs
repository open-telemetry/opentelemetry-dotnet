// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CA2000 // Provider takes ownership of processor/exporter

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Trace;

public class ActivityExportProcessorSelfObservabilityTests
{
    private const string MetricName = "otel.sdk.processor.span.processed";

    [Fact]
    public void BatchProcessor_ReportsQueueSizeAndCapacity()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var exporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 5,
            scheduledDelayMilliseconds: int.MaxValue,
            maxExportBatchSize: 5);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 2);
        meterProvider.ForceFlush();

        var sizePoint = GetMetricPoints(exportedMetrics, "otel.sdk.processor.span.queue.size").Single();
        var capacityPoint = GetMetricPoints(exportedMetrics, "otel.sdk.processor.span.queue.capacity").Single();

        Assert.Equal(2, sizePoint.GetSumLong());
        Assert.Equal(5, capacityPoint.GetSumLong());
        AssertTag(sizePoint, "otel.component.type", "batching_span_processor");
        AssertTagStartsWith(sizePoint, "otel.component.name", "batching_span_processor/");

        Assert.True(processor.ForceFlush());
        exportedMetrics.Clear();
        meterProvider.ForceFlush();

        sizePoint = GetMetricPoints(exportedMetrics, "otel.sdk.processor.span.queue.size").Single();
        Assert.Equal(0, sizePoint.GetSumLong());
    }

    [Fact]
    public async Task BatchProcessor_CountsSuccessWhenSubmittedToExporter()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var exportStarted = new ManualResetEventSlim(false);
        using var allowExport = new ManualResetEventSlim(false);
        using var exporter = new DelegatingExporter<Activity>
        {
            OnExportFunc = batch =>
            {
                exportStarted.Set();
                allowExport.Wait();
                return ExportResult.Success;
            },
        };
        using var processor = new BatchActivityExportProcessor(
            exporter,
            scheduledDelayMilliseconds: int.MaxValue);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 3);

        meterProvider.ForceFlush();
        Assert.DoesNotContain(exportedMetrics, m => m.Name == MetricName);

        var flushTask = Task.Run(() => processor.ForceFlush());
        try
        {
            Assert.True(exportStarted.Wait(TimeSpan.FromSeconds(5)));
            meterProvider.ForceFlush();

            var points = GetMetricPoints(exportedMetrics);
            var successPoint = points.Single(p => !HasTag(p, "error.type"));

            Assert.Equal(3, successPoint.GetSumLong());
            AssertTag(successPoint, "otel.component.type", "batching_span_processor");
            AssertTagStartsWith(successPoint, "otel.component.name", "batching_span_processor/");
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
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        // Use a blocking exporter so the worker thread holds the queue slot
        // while we overflow it, guaranteeing drops.
        using var exportStarted = new ManualResetEventSlim(false);
        using var allowExport = new ManualResetEventSlim(false);
        using var exporter = new DelegatingExporter<Activity>
        {
            OnExportFunc = batch =>
            {
                exportStarted.Set();
                allowExport.Wait();
                return ExportResult.Success;
            },
        };
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 1,
            scheduledDelayMilliseconds: 1,
            maxExportBatchSize: 1);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        // First span triggers the worker; wait for it to block in Export.
        StartAndStopActivities(source, 1);
        Assert.True(exportStarted.Wait(TimeSpan.FromSeconds(5)));

        // Now the queue is being drained but the worker is blocked.
        // Subsequent spans will overflow the queue (size=1).
        StartAndStopActivities(source, 5);

        // Release the exporter so the flush can complete.
        allowExport.Set();
        processor.ForceFlush();

        meterProvider.ForceFlush();

        var points = GetMetricPoints(exportedMetrics);
        var queueFullPoint = points.Single(p => HasTagValue(p, "error.type", "queue_full"));

        Assert.True(queueFullPoint.GetSumLong() > 0);
        Assert.Equal(6, points.Sum(p => p.GetSumLong()));
    }

    [Fact]
    public void BatchProcessor_AfterShutdown()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        var exportedActivities = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedActivities);
        var processor = new BatchActivityExportProcessor(exporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 2);

        processor.Shutdown();

        StartAndStopActivities(source, 1);

        meterProvider.ForceFlush();

        var points = GetMetricPoints(exportedMetrics);
        var shutdownPoint = points.Single(p => HasTagValue(p, "error.type", "already_shutdown"));
        var successPoint = points.Single(p => !HasTag(p, "error.type"));

        Assert.Equal(1, shutdownPoint.GetSumLong());
        Assert.Equal(2, successPoint.GetSumLong());
        Assert.Equal(2, exportedActivities.Count);
    }

    [Fact]
    public void SimpleProcessor_SuccessAndShutdown()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        var exportedActivities = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedActivities);
        var processor = new SimpleActivityExportProcessor(exporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 2);

        processor.Shutdown();

        StartAndStopActivities(source, 1);

        meterProvider.ForceFlush();

        var points = GetMetricPoints(exportedMetrics);

        var successPoint = points.Single(p =>
            HasTagValue(p, "otel.component.type", "simple_span_processor") && !HasTag(p, "error.type"));
        var shutdownPoint = points.Single(p =>
            HasTagValue(p, "otel.component.type", "simple_span_processor") && HasTagValue(p, "error.type", "already_shutdown"));

        Assert.Equal(2, successPoint.GetSumLong());
        Assert.Equal(1, shutdownPoint.GetSumLong());
        Assert.Equal(2, exportedActivities.Count);
        AssertTagStartsWith(successPoint, "otel.component.name", "simple_span_processor/");
    }

    [Fact]
    public void MultipleProcessors_DistinctComponentNames()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var exporter1 = new InMemoryExporter<Activity>(new List<Activity>());
        using var exporter2 = new InMemoryExporter<Activity>(new List<Activity>());
        using var batch1 = new BatchActivityExportProcessor(exporter1);
        using var batch2 = new BatchActivityExportProcessor(exporter2);
        using var simple = new SimpleActivityExportProcessor(exporter1);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(batch1)
            .AddProcessor(batch2)
            .AddProcessor(simple)
            .Build();

        StartAndStopActivities(source, 2);

        batch1.ForceFlush();
        batch2.ForceFlush();
        meterProvider.ForceFlush();

        var points = GetMetricPoints(exportedMetrics);

        // Each processor instance gets a unique otel.component.name, so we expect
        // separate MetricPoints for each: 2 batch + 1 simple = 3 distinct streams.
        var batchPoints = points.Where(p => HasTagValue(p, "otel.component.type", "batching_span_processor")).ToList();
        var simplePoints = points.Where(p => HasTagValue(p, "otel.component.type", "simple_span_processor")).ToList();

        Assert.Equal(2, batchPoints.Count);
        Assert.Single(simplePoints);

        // Each batch processor received the same 2 spans (composite processor fans out).
        Assert.All(batchPoints, p => Assert.Equal(2, p.GetSumLong()));
        Assert.Equal(2, simplePoints[0].GetSumLong());

        // Verify component names are distinct across batch processors.
        var batchNames = batchPoints
            .Select(p => GetTagValue(p, "otel.component.name"))
            .ToList();
        Assert.Equal(2, batchNames.Distinct().Count());
    }

    [Fact]
    public void DroppedSpansDoNotReachProcessorsAndAreNotCounted()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var batchExporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var simpleExporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var batch = new BatchActivityExportProcessor(batchExporter);
        using var simple = new SimpleActivityExportProcessor(simpleExporter);
        var onEndCalls = 0;

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOffSampler())
            .AddProcessor(new TestActivityProcessor(_ => { }, _ => Interlocked.Increment(ref onEndCalls)))
            .AddProcessor(batch)
            .AddProcessor(simple)
            .Build();

        // Root spans dropped by the sampler are still created as PropagationData
        // activities so that trace context propagates, but TracerProviderSdk filters
        // them out (IsAllDataRequested is false) before invoking any processor.
        var activities = StartAndStopActivities(source, 5);
        Assert.Equal(5, activities.Count);
        Assert.All(activities, a =>
        {
            Assert.False(a.Recorded);
            Assert.False(a.IsAllDataRequested);
        });

        batch.ForceFlush();
        meterProvider.ForceFlush();

        Assert.Equal(0, onEndCalls);
        Assert.DoesNotContain(exportedMetrics, m => m.Name == MetricName);
    }

    [Fact]
    public void DroppedSpanPassedDirectlyToOnEndIsNotCounted()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var batchExporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var simpleExporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var batch = new BatchActivityExportProcessor(batchExporter);
        using var simple = new SimpleActivityExportProcessor(simpleExporter);

        // TracerProviderSdk never hands a dropped span to a processor, but the processors
        // are public API and can be invoked directly, so guard that path explicitly.
        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == sourceName,
            Sample = (ref _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);
        Assert.False(activity.Recorded);
        Assert.False(activity.IsAllDataRequested);

        batch.OnEnd(activity);
        simple.OnEnd(activity);

        batch.ForceFlush();
        meterProvider.ForceFlush();

        Assert.DoesNotContain(exportedMetrics, m => m.Name == MetricName);
    }

    // RECORD_ONLY spans are counted as successfully processed per the clarification
    // proposed in https://github.com/open-telemetry/semantic-conventions/pull/3978.
    [Fact]
    public void RecordOnlySpansAreCountedAsSuccess()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        var batchExported = new List<Activity>();
        var simpleExported = new List<Activity>();
        using var batchExporter = new InMemoryExporter<Activity>(batchExported);
        using var simpleExporter = new InMemoryExporter<Activity>(simpleExported);
        using var batch = new BatchActivityExportProcessor(batchExporter);
        using var simple = new SimpleActivityExportProcessor(simpleExporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new RecordOnlySampler())
            .AddProcessor(batch)
            .AddProcessor(simple)
            .Build();

        var activities = StartAndStopActivities(source, 3);
        Assert.Equal(3, activities.Count);
        Assert.All(activities, a =>
        {
            Assert.False(a.Recorded);
            Assert.True(a.IsAllDataRequested);
        });

        batch.ForceFlush();
        meterProvider.ForceFlush();

        var points = GetMetricPoints(exportedMetrics);

        var batchPoint = points.Single(p => HasTagValue(p, "otel.component.type", "batching_span_processor"));
        var simplePoint = points.Single(p => HasTagValue(p, "otel.component.type", "simple_span_processor"));

        Assert.Equal(3, batchPoint.GetSumLong());
        Assert.Equal(3, simplePoint.GetSumLong());
        Assert.False(HasTag(batchPoint, "error.type"));
        Assert.False(HasTag(simplePoint, "error.type"));

        // RECORD_ONLY spans are counted as processed but must never reach an exporter.
        Assert.Empty(batchExported);
        Assert.Empty(simpleExported);
    }

    [Fact]
    public void RecordOnlySpansAreCountedAsSuccessAfterShutdown()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        using var exporter = new InMemoryExporter<Activity>(new List<Activity>());
        var processor = new BatchActivityExportProcessor(exporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new RecordOnlySampler())
            .AddProcessor(processor)
            .Build();

        processor.Shutdown();

        StartAndStopActivities(source, 2);

        meterProvider.ForceFlush();

        // Shutdown does not change the outcome for a RECORD_ONLY span: it was never
        // going to be exported, so nothing is lost and it is not "already_shutdown".
        var points = GetMetricPoints(exportedMetrics);
        var point = points.Single();

        Assert.Equal(2, point.GetSumLong());
        Assert.False(HasTag(point, "error.type"));
    }

    [Fact]
    public void NoListener_NoException()
    {
        // No MeterProvider subscribing to "otel.sdk.experimental" - verifying no exceptions.
        using var exporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var processor = new BatchActivityExportProcessor(exporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 100);
    }

    [Fact]
    public void BatchProcessor_OnEndInvokesOnExportOverride()
    {
        var exportedMetrics = new List<Metric>();
        using var meterProvider = CreateMeterProvider(exportedMetrics);

        var exportedActivities = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedActivities);
        using var processor = new OnExportTrackingBatchActivityExportProcessor(exporter);

        var sourceName = Utils.GetCurrentMethodName();
        using var source = new ActivitySource(sourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(processor)
            .Build();

        StartAndStopActivities(source, 3);
        processor.ForceFlush();
        meterProvider.ForceFlush();

        Assert.Equal(3, processor.OnExportCalls);

        var points = GetMetricPoints(exportedMetrics);
        var success = points.Single(p => !HasTag(p, "error.type"));
        Assert.Equal(3, success.GetSumLong());
    }

    [Fact]
    public void SelfObservabilityInstrumentsDoNotProduceIgnoredInstrumentWarnings()
    {
        using var activitySource = new ActivitySource(new ActivitySourceOptions(Utils.GetCurrentMethodName()));
        using var exporter = new InMemoryExporter<Activity>(new List<Activity>());
        using var processor = new BatchActivityExportProcessor(exporter);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .AddProcessor(processor)
            .Build();

        using (activitySource.StartActivity("Test"))
        {
        }

        // Forces the self-observability instruments to be created.
        processor.ForceFlush();

        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);

        // A meter which is not subscribed to by the provider below. It guarantees at least one
        // MetricInstrumentIgnored warning is emitted, so the assert below is not vacuous.
        using var unsubscribedMeter = new Meter(new MeterOptions(Utils.GetCurrentMethodName() + ".Unsubscribed"));
        unsubscribedMeter.CreateCounter<long>("test.counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(Utils.GetCurrentMethodName())
            .Build();

        var warnings = eventListener.Messages
            .Where(e => e.EventId == 33)
            .Select(e => (string?)e.Payload?[0])
            .ToList();

        Assert.Contains("test.counter", warnings);
        Assert.DoesNotContain(warnings, w => w?.StartsWith("otel.sdk.", StringComparison.Ordinal) == true);
    }

    private static MeterProvider CreateMeterProvider(List<Metric> exportedMetrics)
        => Sdk.CreateMeterProviderBuilder()
            .AddMeter("otel.sdk.experimental")
            .AddInMemoryExporter(exportedMetrics)
            .Build();

    private static List<Activity> StartAndStopActivities(ActivitySource source, int count)
    {
        var activities = new List<Activity>(count);

        for (var i = 0; i < count; i++)
        {
            using var activity = source.StartActivity("test");
            if (activity != null)
            {
                activities.Add(activity);
            }
        }

        return activities;
    }

    private static List<MetricPoint> GetMetricPoints(List<Metric> exportedMetrics)
        => GetMetricPoints(exportedMetrics, MetricName);

    private static List<MetricPoint> GetMetricPoints(List<Metric> exportedMetrics, string metricName)
    {
        var metric = exportedMetrics.Single(m => m.Name == metricName);

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

    private sealed class OnExportTrackingBatchActivityExportProcessor : BatchActivityExportProcessor
    {
        private int onExportCalls;

        public OnExportTrackingBatchActivityExportProcessor(BaseExporter<Activity> exporter)
            : base(exporter)
        {
        }

        public int OnExportCalls => Volatile.Read(ref this.onExportCalls);

        protected override void OnExport(Activity data)
        {
            Interlocked.Increment(ref this.onExportCalls);
            base.OnExport(data);
        }
    }
}
