// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpTraceExporterSpanLimitWarningTests
{
    private const int SpanLimitsExceededEventId = 60;
    private const int InitialBufferSize = 256;

    [Fact]
    public void WriteTraceData_BufferGrowth_ReportsDropsOnceAndPreservesPayload()
    {
        using var listener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);
        using var activity = CreateActivityWithOverLimitAttributes();
        var limiter = new SdkLimitOptions { SpanAttributeCountLimit = 1 }.CreateSpanLimiter("test");

        var buffer = ProtobufSerializer.RentBuffer(InitialBufferSize);
        try
        {
            var initialLength = buffer.Length;
            activity.AddEvent(new ActivityEvent(new string('e', 4 * initialLength)));
            using var batch = new Batch<Activity>([activity], 1);
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, limiter, Resource.Empty, batch);

            Assert.True(buffer.Length > initialLength, $"Expected buffer to grow beyond {initialLength}, but got {buffer.Length}.");
            var request = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, writePosition);
            var span = Assert.Single(Assert.Single(Assert.Single(request.ResourceSpans).ScopeSpans).Spans);
            Assert.Equal("a", Assert.Single(span.Attributes).Key);
            Assert.Equal(2u, span.DroppedAttributesCount);
            Assert.Equal(activity.Events.Select(e => e.Name), span.Events.Select(e => e.Name));
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }

        var warning = Assert.Single(listener.Messages, e => e.EventId == SpanLimitsExceededEventId);
        Assert.Equal(["test", 1L, 2L, 0L, 0L], warning.Payload!);
    }

    [Fact]
    public void WriteTraceData_BufferCannotGrow_DiscardsDropsAndPreservesNextWarning()
    {
        using var listener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);
        listener.EnableEvents(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);
        using var activity = CreateActivityWithOverLimitAttributes();
        var limiter = new SdkLimitOptions { SpanAttributeCountLimit = 1 }.CreateSpanLimiter("test");

        var buffer = ProtobufSerializer.RentBuffer(InitialBufferSize);
        try
        {
            using var batch = new Batch<Activity>([activity], 1);
            var maxBufferSize = buffer.Length;
            activity.AddEvent(new ActivityEvent(new string('e', 4 * maxBufferSize)));
            Assert.Throws<ArgumentException>(
                () => ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, limiter, Resource.Empty, batch, maxBufferSize));
            var capacityError = Assert.Single(
                listener.CurrentMessages,
                e => e.EventSource == OpenTelemetryProtocolExporterEventSource.Log && e.EventId == 14);
            Assert.Equal(["Traces", maxBufferSize], capacityError.Payload!);
            Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == SpanLimitsExceededEventId);

            using var nextActivity = new Activity("next");
            nextActivity.SetTag("kept", 1);
            nextActivity.SetTag("dropped", 2);
            using var nextBatch = new Batch<Activity>([nextActivity], 1);
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, limiter, Resource.Empty, nextBatch);
            var request = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, writePosition);
            var span = Assert.Single(Assert.Single(Assert.Single(request.ResourceSpans).ScopeSpans).Spans);
            Assert.Equal("next", span.Name);
            Assert.Equal("kept", Assert.Single(span.Attributes).Key);
            Assert.Equal(1u, span.DroppedAttributesCount);
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }

        var warning = Assert.Single(listener.CurrentMessages, e => e.EventId == SpanLimitsExceededEventId);
        Assert.Equal(["test", 1L, 1L, 0L, 0L], warning.Payload!);
    }

    [Fact]
    public void Export_LimitsExceeded_WarningNamesExporterWithRedactedEndpoint()
    {
        using var listener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);
        var exporterOptions = new OtlpExporterOptions { Endpoint = new Uri("http://user:secret@collector:4318/v1/traces?token=abc") };
        var exportClient = new TestExportClient();
        using var transmissionHandler = new OtlpExporterTransmissionHandler(exportClient, exporterOptions.TimeoutMilliseconds);
        using var exporter = new OtlpTraceExporter(
            exporterOptions,
            new SdkLimitOptions { SpanAttributeCountLimit = 0 },
            new ExperimentalOptions(),
            transmissionHandler);

        using var activity = new Activity("test");
        activity.SetTag("a", 1);

        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([activity], 1)));

        var warning = Assert.Single(listener.Messages, e => e.EventId == SpanLimitsExceededEventId);
        Assert.Equal("OtlpTraceExporter (http://collector:4318/v1/traces)", warning.Payload![0]);
    }

    [Fact]
    public void Export_RequestExceedsMaxRequestSize_DiscardsDropsAndPreservesNextWarning()
    {
        const int RequestDiscardedEventId = 41;

        using var listener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);
        listener.EnableEvents(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);
        var exportClient = new TestExportClient();
        var exporterOptions = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://collector:4318/v1/traces"),
            Protocol = OtlpExportProtocol.HttpProtobuf,

            // The smallest limit allowed. The pool rounds the rented buffer up past it, so the
            // span below serializes without growing the buffer and is then rejected for its size.
            MaxRequestSizeBytes = OtlpExporterOptions.MinimumMaxRequestSizeBytes,
        };
        using var transmissionHandler = new OtlpExporterTransmissionHandler(exportClient, exporterOptions.TimeoutMilliseconds);
        using var exporter = new OtlpTraceExporter(
            exporterOptions,
            new SdkLimitOptions { SpanAttributeCountLimit = 1 },
            new ExperimentalOptions(),
            transmissionHandler);

        using var oversized = CreateActivityWithOverLimitAttributes();
        oversized.AddEvent(new ActivityEvent(new string('e', OtlpExporterOptions.MinimumMaxRequestSizeBytes + 50_000)));

        Assert.Equal(ExportResult.Failure, exporter.Export(new Batch<Activity>([oversized], 1)));
        Assert.False(exportClient.SendExportRequestCalled, "Expected the export request not to be sent due to exceeding max request size.");
        Assert.Contains(
            listener.CurrentMessages,
            e => e.EventSource == OpenTelemetryProtocolExporterEventSource.Log && e.EventId == RequestDiscardedEventId);
        Assert.DoesNotContain(
            listener.CurrentMessages,
            e => e.EventSource == OpenTelemetrySdkEventSource.Log && e.EventId == SpanLimitsExceededEventId);

        // The rejected request must not use up the warning interval.
        using var next = new Activity("next");
        next.SetTag("kept", 1);
        next.SetTag("dropped", 2);

        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([next], 1)));

        var warning = Assert.Single(
            listener.CurrentMessages,
            e => e.EventSource == OpenTelemetrySdkEventSource.Log && e.EventId == SpanLimitsExceededEventId);
        Assert.Equal(["OtlpTraceExporter (http://collector:4318/v1/traces)", 1L, 1L, 0L, 0L], warning.Payload!);
    }

    [Fact]
    public void WriteTraceData_MixedDrops_WarningMatchesSerializedCounters()
    {
        using var listener = new TestEventListener(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);
        var limiter = new SdkLimitOptions
        {
            SpanAttributeCountLimit = 1,
            SpanEventCountLimit = 1,
            SpanLinkCountLimit = 1,
            SpanEventAttributeCountLimit = 1,
            SpanLinkAttributeCountLimit = 1,
        }.CreateSpanLimiter("test");
        using var first = new Activity("first");
        first.SetTag("a", 1);
        first.SetTag("b", 2);
        first.AddEvent(new ActivityEvent("kept", tags: new ActivityTagsCollection { ["a"] = 1, ["b"] = 2, ["c"] = 3 }));
        first.AddEvent(new ActivityEvent("dropped", tags: new ActivityTagsCollection { ["ignored"] = 1 }));
        first.AddLink(new ActivityLink(default, new ActivityTagsCollection { ["a"] = 1, ["b"] = 2, ["c"] = 3, ["d"] = 4 }));
        first.AddLink(new ActivityLink(default, new ActivityTagsCollection { ["ignored"] = 1 }));
        using var second = new Activity("second");
        second.SetTag("a", 1);
        second.SetTag("b", 2);
        using var clean = new Activity("clean");
        using var batch = new Batch<Activity>([first, second, clean], 3);

        var buffer = ProtobufSerializer.RentBuffer(InitialBufferSize);
        try
        {
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, limiter, Resource.Empty, batch);
            var request = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, writePosition);
            var spans = Assert.Single(Assert.Single(request.ResourceSpans).ScopeSpans).Spans;
            Assert.Equal(["first", "second", "clean"], spans.Select(span => span.Name));
            Assert.Equal(1u, spans[0].DroppedAttributesCount);
            Assert.Equal(2u, Assert.Single(spans[0].Events).DroppedAttributesCount);
            Assert.Equal(3u, Assert.Single(spans[0].Links).DroppedAttributesCount);
            Assert.Equal(1u, spans[0].DroppedEventsCount);
            Assert.Equal(1u, spans[0].DroppedLinksCount);

            var droppedAttributes = spans.Sum(span =>
                span.DroppedAttributesCount
                + span.Events.Sum(e => (long)e.DroppedAttributesCount)
                + span.Links.Sum(link => (long)link.DroppedAttributesCount));
            var warning = Assert.Single(listener.CurrentMessages, e => e.EventId == SpanLimitsExceededEventId);
            Assert.Equal(["test", 2L, droppedAttributes, 1L, 1L], warning.Payload!);
            Assert.Equal(7, droppedAttributes);
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }

    private static Activity CreateActivityWithOverLimitAttributes()
    {
        var activity = new Activity("test");
        activity.SetTag("a", 1);
        activity.SetTag("b", 2);
        activity.SetTag("c", 3);
        return activity;
    }
}
