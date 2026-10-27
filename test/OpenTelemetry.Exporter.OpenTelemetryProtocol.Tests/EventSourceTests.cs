// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
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

    [Theory]
    [InlineData(
        "https://user:secret@example.com:4318/ingest/token/v1/traces?api-key=abc123#fragment",
        "https://example.com:4318/[REDACTED]")]
    [InlineData(
        "https://user:secret@example.com/ingest/token/v1/metrics?api-key=abc123#fragment",
        "https://example.com/[REDACTED]")]
    [InlineData(
        "http://user:secret@[::1]:4318/ingest/token/v1/logs?api-key=abc123#fragment",
        "http://[::1]:4318/[REDACTED]")]
    [InlineData(
        "http://example.com/ingest/token/opentelemetry.proto.collector.trace.v1.TraceService/Export",
        "http://example.com/[REDACTED]")]
    [InlineData("otel-user:S3cretPass@127.0.0.1:9/v1/traces", "otel-user:[REDACTED]")]
    [InlineData("http://example.com:4318", "http://example.com:4318")]
    [InlineData("https://example.com/v1/traces", "https://example.com/v1/traces")]
    [InlineData(
        "https://user:secret@example.com:4318/v1/traces?api-key=abc123#fragment",
        "https://example.com:4318/v1/traces")]
    [InlineData(
        "https://user:secret@example.com:443/v1/metrics?api-key=abc123#fragment",
        "https://example.com/v1/metrics")]
    [InlineData(
        "http://user:secret@[::1]:4318/v1/logs?api-key=abc123#fragment",
        "http://[::1]:4318/v1/logs")]
    [InlineData(
        "https://user:secret@example.com/"
        + "opentelemetry.proto.collector.trace.v1.TraceService/Export?api-key=abc123#fragment",
        "https://example.com/opentelemetry.proto.collector.trace.v1.TraceService/Export")]
    [InlineData(
        "https://user:secret@example.com:4317/"
        + "opentelemetry.proto.collector.metrics.v1.MetricsService/Export?api-key=abc123#fragment",
        "https://example.com:4317/opentelemetry.proto.collector.metrics.v1.MetricsService/Export")]
    [InlineData(
        "https://user:secret@example.com/"
        + "opentelemetry.proto.collector.logs.v1.LogsService/Export?api-key=abc123#fragment",
        "https://example.com/opentelemetry.proto.collector.logs.v1.LogsService/Export")]
    [InlineData("https://example.com/v1/trace", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com/V1/traces", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com/v1/traces/", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com/v1/traces/secret", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com/v1%2Ftraces", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com/secret%2Ftoken/v1/traces", "https://example.com/[REDACTED]")]
    [InlineData("https://example.com//v1/traces", "https://example.com/[REDACTED]")]
    [InlineData("https://user:secret@example.com:443/?api-key=abc123#fragment", "https://example.com")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/v1/traces%0Asecret", "https://example.com/[REDACTED]")]
    public void EventSourceTests_OpenTelemetryProtocolExporterEventSource_RedactsSensitiveEndpointComponents(
        string endpoint,
        string expectedEndpoint)
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose, EventKeywords.All);

        var uri = new Uri(endpoint);
        var exception = new HttpRequestException("boom");
        var log = OpenTelemetryProtocolExporterEventSource.Log;

        log.FailedToReachCollector(uri, exception);
        log.TransientHttpError(uri, exception);
        log.HttpRequestFailed(uri, "response", exception);
        log.OperationUnexpectedlyCanceled(uri, exception);
        log.RequestTimedOut(uri, exception);
        log.ResponseDeserializationFailed(uri);
        log.ExportSuccess(uri, "success");
        log.ExportFailure(uri, "failure", Status.DefaultCancelled);
        log.ResponseDiscardedDueToSizeLimit(uri, 1024, 512);

        var messages = listener.CurrentMessages;
        Assert.Equal([2, 16, 17, 18, 19, 20, 21, 23, 40], messages.Select(message => message.EventId));

        Assert.All(messages, message =>
        {
            Assert.NotNull(message.Payload);
            Assert.NotEmpty(message.Payload);
            Assert.Equal(expectedEndpoint, Assert.IsType<string>(message.Payload[0]));
        });
    }
}
