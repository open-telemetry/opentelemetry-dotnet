// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetryProtocolExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryProtocolExporterEventSource>();

    [Fact]
    public void EventSourceTests_PersistentStorageAbstractionsEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageAbstractionsEventSource>();

    [Fact]
    public void EventSourceTests_PersistentStorageEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageEventSource>();

    [Fact]
    public void EventSourceTests_OpenTelemetryProtocolExporterEventSource_RedactsSensitiveEndpointComponents()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose, EventKeywords.All);

        OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(
            new Uri("https://user:secret@example.com:4318/v1/traces?api-key=abc123#fragment"),
            new HttpRequestException("boom"));

        var exportFailureEvent = Assert.Single(listener.Messages, e => e.EventId == 2);

        Assert.NotNull(exportFailureEvent.Payload);
        Assert.NotEmpty(exportFailureEvent.Payload);

        var endpoint = Assert.IsType<string>(exportFailureEvent.Payload[0]);

        Assert.Equal("https://example.com:4318/v1/traces", endpoint);
    }
}
