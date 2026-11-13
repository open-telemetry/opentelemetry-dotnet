// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

[Collection(EventSourceCollectionDefinition.Name)]
public class OtlpPartialSuccessTests
{
    private const int ExportSuccessEventId = 21;
    private const int ResponseDiscardedEventId = 40;
    private const int ExportPartiallySucceededEventId = 45;
    private const int ExportSucceededWithWarningEventId = 46;

    private const int MaxResponseSizeBytes = 1024;

    private static readonly byte[] GrpcRequest = [0, 0, 0, 0, 0];

    public static TheoryData<bool, int, string> SignalTypes()
    {
        var data = new TheoryData<bool, int, string>();

        foreach (var grpc in new[] { true, false })
        {
            data.Add(grpc, (int)OtlpSignalType.Traces, "span(s)");
            data.Add(grpc, (int)OtlpSignalType.Metrics, "data point(s)");
            data.Add(grpc, (int)OtlpSignalType.Logs, "log record(s)");
        }

        return data;
    }

    public static TheoryData<bool, byte[]> MalformedBodies()
    {
        var data = new TheoryData<bool, byte[]>();
        var truncated = PartialSuccessResponse.Create(5, "message");

        foreach (var grpc in new[] { true, false })
        {
            // Truncated protobuf message.
            var message = truncated.AsSpan(0, truncated.Length - 3).ToArray();
            data.Add(grpc, grpc ? PartialSuccessResponse.CreateGrpcFrame(message) : message);

            // Not a protobuf message.
            var garbage = "<html>bad gateway</html>"u8.ToArray();
            data.Add(grpc, grpc ? PartialSuccessResponse.CreateGrpcFrame(garbage) : garbage);
        }

        // gRPC frame header cut short.
        data.Add(true, [0, 0, 0]);

        // gRPC frame declaring more bytes than it has.
        data.Add(true, PartialSuccessResponse.CreateGrpcFrame(truncated, declaredLength: (uint)truncated.Length + 10));

        // A compressed gRPC message, which was not asked for and cannot be read.
        data.Add(true, PartialSuccessResponse.CreateGrpcFrame(truncated, compressionFlag: 1));

        return data;
    }

    [Theory]
    [MemberData(nameof(SignalTypes))]
    public void SendExportRequest_RejectedItems_SucceedsAndReportsRejection(bool grpc, int signalType, string expectedItemsName)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var response = Export(grpc, PartialSuccessResponse.Create(rejected: 3, errorMessage: "3 items were invalid"), (OtlpSignalType)signalType, out var requestCount);

        Assert.True(response.Success);
        Assert.Equal(1, requestCount);

        var message = Assert.Single(listener.Messages, e => e.EventId == ExportPartiallySucceededEventId);
        Assert.Equal(EventLevel.Warning, message.Level);
        Assert.NotNull(message.Payload);
        Assert.Equal(expectedItemsName, message.Payload[1]);
        Assert.Equal(3L, message.Payload[2]);
        Assert.Equal("3 items were invalid", message.Payload[3]);

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == ExportSucceededWithWarningEventId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_RejectedItemsWithoutMessage_ReportsRejection(bool grpc)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var response = Export(grpc, PartialSuccessResponse.Create(rejected: 2, errorMessage: null), OtlpSignalType.Traces, out _);

        Assert.True(response.Success);

        var message = Assert.Single(listener.Messages, e => e.EventId == ExportPartiallySucceededEventId);
        Assert.NotNull(message.Payload);
        Assert.Equal(2L, message.Payload[2]);
        Assert.Equal(string.Empty, message.Payload[3]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_UnknownSignal_NamesItemsGenerically(bool grpc)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        Export(grpc, PartialSuccessResponse.Create(rejected: 1, errorMessage: "x"), signalType: null, out _);

        var message = Assert.Single(listener.Messages, e => e.EventId == ExportPartiallySucceededEventId);
        Assert.NotNull(message.Payload);
        Assert.Equal("item(s)", message.Payload[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_WarningWithoutRejectedItems_SucceedsAndReportsWarning(bool grpc)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var response = Export(grpc, PartialSuccessResponse.Create(rejected: 0, errorMessage: "deprecated attribute"), OtlpSignalType.Logs, out _);

        Assert.True(response.Success);

        var message = Assert.Single(listener.Messages, e => e.EventId == ExportSucceededWithWarningEventId);
        Assert.Equal(EventLevel.Warning, message.Level);
        Assert.NotNull(message.Payload);
        Assert.Equal("deprecated attribute", message.Payload[1]);

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == ExportPartiallySucceededEventId);
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(true, 0L, null)]
    [InlineData(true, 0L, "")]
    [InlineData(false, null, null)]
    [InlineData(false, 0L, null)]
    [InlineData(false, 0L, "")]
    public void SendExportRequest_EmptyPartialSuccess_IsPlainSuccess(bool grpc, long? rejected, string? errorMessage)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose);

        var response = Export(grpc, PartialSuccessResponse.Create(rejected, errorMessage), OtlpSignalType.Metrics, out _);

        Assert.True(response.Success);
        Assert.Single(listener.Messages, e => e.EventId == ExportSuccessEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_EmptyBody_IsPlainSuccess(bool grpc)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose);

        var response = ExportRaw(grpc, grpc ? PartialSuccessResponse.CreateGrpcFrame([]) : [], OtlpSignalType.Traces, out _);

        Assert.True(response.Success);
        Assert.Single(listener.Messages, e => e.EventId == ExportSuccessEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public void SendExportRequest_MalformedBody_DoesNotFailExport(bool grpc, byte[] body)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose);

        var response = ExportRaw(grpc, body, OtlpSignalType.Traces, out var requestCount);

        Assert.True(response.Success);
        Assert.Null(response.Exception);
        Assert.Equal(1, requestCount);
        Assert.Single(listener.Messages, e => e.EventId == ExportSuccessEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public void SendExportRequest_HttpNonProtobufBody_IsIgnored(string mediaType)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose);

        // A body that would be reported if it were read.
        var body = PartialSuccessResponse.Create(rejected: 4, errorMessage: "x");
        using var handler = new PartialSuccessHttpMessageHandler(body, grpc: false, mediaType);

        var response = CreateClient(grpc: false, handler, OtlpSignalType.Traces, out var httpClient)
            .SendExportRequest(GrpcRequest, GrpcRequest.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);
        httpClient.Dispose();

        Assert.True(response.Success);
        Assert.Single(listener.Messages, e => e.EventId == ExportSuccessEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Fact]
    public void SendExportRequest_HttpBodyWithoutMediaType_IsReadAsProtobuf()
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var body = PartialSuccessResponse.Create(rejected: 4, errorMessage: "x");
        using var handler = new PartialSuccessHttpMessageHandler(body, grpc: false, mediaType: null);

        var response = CreateClient(grpc: false, handler, OtlpSignalType.Traces, out var httpClient)
            .SendExportRequest(GrpcRequest, GrpcRequest.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);
        httpClient.Dispose();

        Assert.True(response.Success);
        Assert.Single(listener.Messages, e => e.EventId == ExportPartiallySucceededEventId);
    }

    [Fact]
    public void SendExportRequest_HttpUndeclaredOversizedBody_IsNotReadBeyondLimit()
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        // A valid partial success that is larger than the limit, sent without a Content-Length.
        var body = PartialSuccessResponse.Create(rejected: 1, errorMessage: new string('x', MaxResponseSizeBytes));
        using var handler = new PartialSuccessHttpMessageHandler(body, grpc: false, declareLength: false);

        var response = CreateClient(grpc: false, handler, OtlpSignalType.Traces, out var httpClient)
            .SendExportRequest(GrpcRequest, GrpcRequest.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);
        httpClient.Dispose();

        Assert.True(response.Success);
        Assert.Equal(1, handler.RequestCount);
        Assert.Single(listener.Messages, e => e.EventId == ResponseDiscardedEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Fact]
    public void SendExportRequest_GrpcFrameDeclaringOversizedMessage_IsNotBuffered()
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        // The frame claims a message far larger than the limit, which must not be allocated.
        var message = PartialSuccessResponse.Create(rejected: 1, errorMessage: "x");
        var body = PartialSuccessResponse.CreateGrpcFrame(message, declaredLength: int.MaxValue);

        var response = ExportRaw(grpc: true, body, OtlpSignalType.Traces, out var requestCount);

        Assert.True(response.Success);
        Assert.Equal(1, requestCount);

        var discarded = Assert.Single(listener.Messages, e => e.EventId == ResponseDiscardedEventId);
        Assert.NotNull(discarded.Payload);
        Assert.Equal((long)int.MaxValue, discarded.Payload[1]);
        AssertNoPartialSuccessEvents(listener);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_DeclaredOversizedBody_IsNotRetried(bool grpc)
    {
        // A response whose declared size exceeds the limit is discarded before it is read.
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var body = PartialSuccessResponse.Create(rejected: 1, errorMessage: new string('x', MaxResponseSizeBytes + 10));
        using var handler = new PartialSuccessHttpMessageHandler(grpc ? PartialSuccessResponse.CreateGrpcFrame(body) : body, grpc);

        var exportClient = CreateClient(grpc, handler, OtlpSignalType.Traces, out var httpClient);
        using (httpClient)
        {
            using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 10_000);

            transmissionHandler.TrySubmitRequest(GrpcRequest, GrpcRequest.Length);
        }

        Assert.Equal(1, handler.RequestCount);
        Assert.Single(listener.Messages, e => e.EventId == ResponseDiscardedEventId);
        AssertNoPartialSuccessEvents(listener);
    }

    [Theory]
    [InlineData(true, 5L, "rejected")]
    [InlineData(false, 5L, "rejected")]
    [InlineData(true, 0L, "warning")]
    [InlineData(false, 0L, "warning")]
    public void TrySubmitRequest_PartialSuccess_IsNotRetried(bool grpc, long rejected, string errorMessage)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var body = PartialSuccessResponse.Create(rejected, errorMessage);
        using var handler = new PartialSuccessHttpMessageHandler(grpc ? PartialSuccessResponse.CreateGrpcFrame(body) : body, grpc);

        var exportClient = CreateClient(grpc, handler, OtlpSignalType.Traces, out var httpClient);
        using (httpClient)
        {
            using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 10_000);

            Assert.True(transmissionHandler.TrySubmitRequest(GrpcRequest, GrpcRequest.Length));
        }

        Assert.Equal(1, handler.RequestCount);
        Assert.Single(
            listener.Messages,
            e => e.EventId is ExportPartiallySucceededEventId or ExportSucceededWithWarningEventId);
    }

    [Theory]
    [InlineData(true, 1L, ExportPartiallySucceededEventId, 3)]
    [InlineData(false, 1L, ExportPartiallySucceededEventId, 3)]
    [InlineData(true, 0L, ExportSucceededWithWarningEventId, 1)]
    [InlineData(false, 0L, ExportSucceededWithWarningEventId, 1)]
    public void SendExportRequest_LongErrorMessage_IsTruncated(bool grpc, long rejected, int expectedEventId, int messageIndex)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        // Larger than the ETW event size limit once encoded as UTF-16.
        var errorMessage = new string('x', 50_000);

        var response = Export(grpc, PartialSuccessResponse.Create(rejected, errorMessage), OtlpSignalType.Traces, out _, maxResponseSizeBytes: 1024 * 1024);

        Assert.True(response.Success);

        var message = Assert.Single(listener.Messages, e => e.EventId == expectedEventId);
        Assert.NotNull(message.Payload);
        Assert.Equal(
            new string('x', OtlpExportClient.MaxLoggedErrorMessageLength) + "[TRUNCATED]",
            message.Payload[messageIndex]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void TruncateErrorMessage_ShortMessage_IsUnchanged(string? errorMessage)
        => Assert.Same(errorMessage, OtlpExportClient.TruncateErrorMessage(errorMessage));

    [Fact]
    public void TruncateErrorMessage_MessageAtLimit_IsUnchanged()
    {
        var errorMessage = new string('x', OtlpExportClient.MaxLoggedErrorMessageLength);

        Assert.Same(errorMessage, OtlpExportClient.TruncateErrorMessage(errorMessage));
    }

    [Fact]
    public void TruncateErrorMessage_DoesNotSplitSurrogatePair()
    {
        // A surrogate pair straddling the limit is dropped as a whole.
        var errorMessage = new string('x', OtlpExportClient.MaxLoggedErrorMessageLength - 1) + "\U0001F600" + "tail";

        var truncated = OtlpExportClient.TruncateErrorMessage(errorMessage);

        Assert.Equal(new string('x', OtlpExportClient.MaxLoggedErrorMessageLength - 1) + "[TRUNCATED]", truncated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_UnreadableBodyWithoutListener_Succeeds(bool grpc)
    {
        // Without a listener the body is not read at all, and with one it is ignored.
        var response = ExportRaw(grpc, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], OtlpSignalType.Traces, out var requestCount);

        Assert.True(response.Success);
        Assert.Equal(1, requestCount);
    }

    private static ExportClientResponse Export(bool grpc, byte[] message, OtlpSignalType? signalType, out int requestCount, int maxResponseSizeBytes = MaxResponseSizeBytes)
        => ExportRaw(grpc, grpc ? PartialSuccessResponse.CreateGrpcFrame(message) : message, signalType, out requestCount, maxResponseSizeBytes);

    private static ExportClientResponse ExportRaw(bool grpc, byte[] body, OtlpSignalType? signalType, out int requestCount, int maxResponseSizeBytes = MaxResponseSizeBytes)
    {
        using var handler = new PartialSuccessHttpMessageHandler(body, grpc);

        var exportClient = CreateClient(grpc, handler, signalType, out var httpClient, maxResponseSizeBytes);

        using (httpClient)
        {
            var response = exportClient.SendExportRequest(GrpcRequest, GrpcRequest.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);
            requestCount = handler.RequestCount;
            return response;
        }
    }

    private static OtlpExportClient CreateClient(bool grpc, HttpMessageHandler handler, OtlpSignalType? signalType, out HttpClient httpClient, int maxResponseSizeBytes = MaxResponseSizeBytes)
    {
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(grpc ? "http://localhost:4317" : "http://localhost:4318/v1/traces"),
            Protocol = grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf,
            MaxResponseSizeBytes = maxResponseSizeBytes,
        };
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        httpClient = new HttpClient(handler, disposeHandler: false);

        return grpc
            ? new OtlpGrpcExportClient(options, httpClient, "opentelemetry.proto.collector.trace.v1.TraceService/Export", signalType)
            : new OtlpHttpExportClient(options, httpClient, "v1/traces", signalType);
    }

    private static void AssertNoPartialSuccessEvents(TestEventListener listener) =>
        Assert.DoesNotContain(
            listener.CurrentMessages,
            e => e.EventId is ExportPartiallySucceededEventId or ExportSucceededWithWarningEventId);
}
