// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_PrometheusExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PrometheusExporterEventSource>();

    [Fact]
    public void EventSource_EmitsEventsWhenInvoked()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(PrometheusExporterEventSource.Log, EventLevel.Verbose, EventKeywords.All);

        var log = PrometheusExporterEventSource.Log;
        var exception = new InvalidOperationException("Something went wrong.");

        log.FailedExport(exception);
        log.CanceledExport(exception);
        log.FailedShutdown(exception);
        log.ConflictingType("test_metric", PrometheusType.Counter, PrometheusType.Gauge);
        log.ConflictingHelp("test_metric", "First help.", "Second help.");
        log.ConflictingUnit("test_metric", "bytes", "seconds");
        log.CollectFailed();

        var events = listener.Messages;

        Assert.Contains(events, e => e.EventId == 1); // FailedExport
        Assert.Contains(events, e => e.EventId == 2); // CanceledExport
        Assert.Contains(events, e => e.EventId == 3); // FailedShutdown
        Assert.Contains(events, e => e.EventId == 12); // CollectFailed

        var conflictingType = Assert.Single(events, e => e.EventId == 6);
        Assert.NotNull(conflictingType.Payload);
        Assert.Equal("test_metric", conflictingType.Payload[0]);
        Assert.Equal(nameof(PrometheusType.Counter), conflictingType.Payload[1]);
        Assert.Equal(nameof(PrometheusType.Gauge), conflictingType.Payload[2]);

        var conflictingHelp = Assert.Single(events, e => e.EventId == 7);
        Assert.NotNull(conflictingHelp.Payload);
        Assert.Equal("test_metric", conflictingHelp.Payload[0]);
        Assert.Equal("First help.", conflictingHelp.Payload[1]);
        Assert.Equal("Second help.", conflictingHelp.Payload[2]);

        var conflictingUnit = Assert.Single(events, e => e.EventId == 8);
        Assert.NotNull(conflictingUnit.Payload);
        Assert.Equal("test_metric", conflictingUnit.Payload[0]);
        Assert.Equal("bytes", conflictingUnit.Payload[1]);
        Assert.Equal("seconds", conflictingUnit.Payload[2]);
    }

    [Fact]
    public void EventSource_MetricIgnored_EmitsMetricNameAndType()
    {
        using var meter = new Meter(nameof(this.EventSource_MetricIgnored_EmitsMetricNameAndType));
        var exportedMetrics = new List<Metric>();

        using (var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(instrument => new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(exportedMetrics)
            .Build())
        {
            meter.CreateHistogram<long>("exponential_histogram").Record(1);
            provider.ForceFlush();
        }

        var metric = exportedMetrics.First(m => m.MetricType == MetricType.ExponentialHistogram);

        using var listener = new TestEventListener();
        listener.EnableEvents(PrometheusExporterEventSource.Log, EventLevel.Verbose, EventKeywords.All);

        PrometheusExporterEventSource.Log.MetricIgnored(metric);

        var metricIgnored = Assert.Single(listener.Messages, e => e.EventId == 10);

        Assert.NotNull(metricIgnored.Payload);
        Assert.Equal("exponential_histogram", metricIgnored.Payload[0]);
        Assert.Equal(nameof(MetricType.ExponentialHistogram), metricIgnored.Payload[1]);
    }
}
