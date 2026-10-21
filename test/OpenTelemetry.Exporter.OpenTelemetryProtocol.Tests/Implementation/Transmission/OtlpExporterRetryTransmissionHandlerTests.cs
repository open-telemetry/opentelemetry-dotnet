// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission.Tests;

public class OtlpExporterRetryTransmissionHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrySubmitRequest_StalledRetry_IsCanceledAtExportDeadline(bool useGrpc)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log);
        using var httpHandler = new RetriedRequestHttpMessageHandler(useGrpc, stallRetry: true);
        using var httpClient = new HttpClient(httpHandler, disposeHandler: false)
        {
            // Each request may take much longer than the total export budget.
            Timeout = TimeSpan.FromSeconds(30),
        };
        var exportClient = CreateExportClient(useGrpc, httpClient);

        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 5_000);

        var result = transmissionHandler.TrySubmitRequest([0, 0, 0, 0, 0], 5);

        Assert.False(result);
        Assert.Equal(2, httpHandler.SendCount);

        // The stalled retry only waits a bounded time for cancellation, so this also asserts that
        // the deadline canceled it. The elapsed time is not asserted, as the cancellation callback
        // can run seconds late on a busy machine.
        Assert.True(httpHandler.RetryCanceled, "The stalled retry must be canceled at the original export deadline.");

        var timedOutEvent = Assert.Single(listener.Messages, e => e.EventName == "RequestTimedOut");
        Assert.Equal(EventLevel.Warning, timedOutEvent.Level);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrySubmitRequest_RetryCompletesBeforeDeadline_Succeeds(bool useGrpc)
    {
        using var httpHandler = new RetriedRequestHttpMessageHandler(useGrpc, stallRetry: false);
        using var httpClient = new HttpClient(httpHandler, disposeHandler: false);
        var exportClient = CreateExportClient(useGrpc, httpClient);
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 10_000);

        var result = transmissionHandler.TrySubmitRequest([0, 0, 0, 0, 0], 5);

        Assert.True(result);
        Assert.Equal(2, httpHandler.SendCount);
        Assert.False(httpHandler.RetryCanceled);
    }

    [Fact]
    public void TrySubmitRequest_HttpTimeoutWithExpiredDeadline_DoesNotRetry()
    {
        // When HttpClient.Timeout fires, deadlineUtc is already expired. To stay within the
        // OTLP Timeout contract (max wait per batch export), the handler must not retry when
        // the deadline is exhausted.
        var exportClient = new TimeoutThenSuccessExportClient();
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 100_000);

        var result = transmissionHandler.TrySubmitRequest([1, 2, 3], 3);

        Assert.False(result, "Should not retry when the deadline is already expired.");
        Assert.Equal(1, exportClient.SendCount);
    }

    [Fact]
    public void TrySubmitRequest_HttpNoStatusFailureWithRemainingDeadline_Retries()
    {
        // When a no-status failure occurs before the deadline is exhausted (e.g. a fast
        // connection reset), the handler should retry within the remaining deadline budget.
        var exportClient = new EarlyFailureThenSuccessExportClient();
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 100_000);

        var result = transmissionHandler.TrySubmitRequest([1, 2, 3], 3);

        Assert.True(result, "Should succeed after retrying the no-status failure.");
        Assert.Equal(2, exportClient.SendCount);
    }

    [Fact]
    public void TrySubmitRequest_PersistentHttpNoStatusFailureWithExpiredDeadline_StopsAfterFirstAttempt()
    {
        // When every attempt returns a no-status failure with an already-expired deadline,
        // the handler must give up immediately rather than retrying indefinitely.
        var exportClient = new PersistentTimeoutExportClient();
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 100_000);

        var result = transmissionHandler.TrySubmitRequest([1, 2, 3], 3);

        Assert.False(result);
        Assert.Equal(1, exportClient.SendCount);
    }

    [Theory]
    [InlineData(int.MaxValue / 2)]
    [InlineData(int.MaxValue - 1024)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(int.MaxValue)]
    [InlineData(uint.MaxValue)]
    public void TrySubmitRequest_FailedRequestWithOverflowingRetryHeader_LogsParsingFailureAndRetries(long value)
    {
        var maliciousHeader = CreateMalformedGrpcStatusDetailsHeader(value);
        var exportClient = new RetryingTestExportClient(maliciousHeader);

        // The timeout must be comfortably larger than OtlpRetry.InitialBackoffMilliseconds (1000ms).
        // Otherwise the random retry backoff (drawn from [0, InitialBackoffMilliseconds)) can exceed
        // the remaining deadline budget, in which case the handler does not retry at all and the test
        // flakes with "Expected at least 2 send attempts, but got 1.".
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds: 100_000);

        bool actual;

#if NET
        using (new AllocationAssertion())
#endif
        {
            actual = transmissionHandler.TrySubmitRequest([1, 2, 3], 3);
        }

        Assert.False(actual);
        Assert.True(exportClient.SendCount >= 2, $"Expected at least 2 send attempts, but got {exportClient.SendCount}.");
    }

    [Theory]
    [InlineData(10_000, Timeout.Infinite, 10_000)]
    [InlineData(10_000, 0, 10_000)]
    [InlineData(10_000, 500, 500)]
    [InlineData(10_000, 10_000, 10_000)]
    [InlineData(10_000, 30_000, 10_000)]
    [InlineData(Timeout.Infinite, Timeout.Infinite, Timeout.Infinite)]
    [InlineData(Timeout.Infinite, 500, 500)]
    public void TrySubmitRequest_DeadlineIsTheShorterOfTheTimeouts(int exporterTimeoutMilliseconds, int timeoutMilliseconds, int expectedTimeoutMilliseconds)
    {
        var exportClient = new TestExportClient();
        using var transmissionHandler = new OtlpExporterRetryTransmissionHandler(exportClient, exporterTimeoutMilliseconds);

        var before = DateTime.UtcNow;
        var result = transmissionHandler.TrySubmitRequest([1, 2, 3], 3, timeoutMilliseconds);
        var after = DateTime.UtcNow;

        Assert.True(result);
        Assert.InRange(
            exportClient.LastDeadlineUtc,
            before.AddMilliseconds(expectedTimeoutMilliseconds),
            after.AddMilliseconds(expectedTimeoutMilliseconds));
    }

    private static IExportClient CreateExportClient(bool useGrpc, HttpClient httpClient)
    {
        var options = new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4318") };
        return useGrpc
            ? new OtlpGrpcExportClient(options, httpClient, string.Empty)
            : new OtlpHttpExportClient(options, httpClient, string.Empty);
    }

    private static string CreateMalformedGrpcStatusDetailsHeader(long value)
    {
        var anyValueLength = EncodeVarint(value);
        var statusBytes = new byte[2 + 1 + anyValueLength.Length];

        statusBytes[0] = 0x1A; // field 3 (details), wire type 2
        statusBytes[1] = (byte)(1 + anyValueLength.Length); // embedded Any payload length
        statusBytes[2] = 0x12; // field 2 (value), wire type 2

        anyValueLength.CopyTo(statusBytes, 3);

        return Convert.ToBase64String(statusBytes);
    }

    private static byte[] EncodeVarint(long value)
    {
        var encoded = new List<byte>();
        var remaining = unchecked((ulong)value);

        do
        {
            var current = (byte)(remaining & 0x7F);
            remaining >>= 7;

            if (remaining != 0)
            {
                current |= 0x80;
            }

            encoded.Add(current);
        }
        while (remaining != 0);

        return [.. encoded];
    }

    private sealed class RetriedRequestHttpMessageHandler(bool useGrpc, bool stallRetry) : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        public bool RetryCanceled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.SendRequest(request, cancellationToken));

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.SendRequest(request, cancellationToken);
#endif

        private HttpResponseMessage SendRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.SendCount++;

            if (this.SendCount > 1 && stallRetry)
            {
                // Bound the wait so the unpatched implementation fails instead of hanging.
                this.RetryCanceled = cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                cancellationToken.ThrowIfCancellationRequested();
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new EmptyContent(),
            };
            if (useGrpc)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
#if NETFRAMEWORK
                response.EnsureTrailingHeaders();
#endif
                var trailingHeaders = response.TrailingHeaders();
                trailingHeaders.Add("grpc-status", this.SendCount == 1 ? "8" : "0");
                if (this.SendCount == 1)
                {
                    var status = new Google.Rpc.Status
                    {
                        Code = 8,
                        Details = { Any.Pack(new Google.Rpc.RetryInfo { RetryDelay = new Duration { Seconds = 1 } }) },
                    };
                    trailingHeaders.Add(OtlpGrpcExportClient.GrpcStatusDetailsHeader, Convert.ToBase64String(status.ToByteArray()));
                }
            }
            else if (this.SendCount == 1)
            {
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            }

            return response;
        }
    }

    /// <summary>
    /// Empty content which is buffered synchronously. On .NET Framework, buffering a
    /// <see cref="ByteArrayContent"/> (as the gRPC client's <see cref="HttpCompletionOption.ResponseContentRead"/>
    /// does) writes through <see cref="Stream.BeginWrite"/>, which runs on the thread pool.
    /// </summary>
    private sealed class EmptyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }

    private sealed class RetryingTestExportClient(string maliciousHeader) : IExportClient
    {
        public int SendCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            this.SendCount++;

            // The first attempt returns a retryable status so the handler parses the malformed
            // status details header and schedules a retry. Subsequent attempts return a
            // non-retryable status so the retry loop terminates deterministically after a single
            // retry, instead of looping until the deadline elapses.
            var retryable = this.SendCount == 1;

            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: null,
                status: new Status(retryable ? StatusCode.Unavailable : StatusCode.Internal, retryable ? "retryable" : "non-retryable"),
                grpcStatusDetailsHeader: maliciousHeader);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

    /// <summary>
    /// Simulates a server that times out on the first request (returning an already-expired
    /// deadline as HttpClient.Timeout would) and succeeds on the second.
    /// The retry handler must not retry since the deadline is exhausted.
    /// </summary>
    private sealed class TimeoutThenSuccessExportClient : IExportClient
    {
        public int SendCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            this.SendCount++;

            if (this.SendCount == 1)
            {
                // Simulate HttpClient.Timeout: the deadline was set to now+timeout before the
                // request, and is now expired because the timeout consumed the full budget.
                return new ExportClientHttpResponse(
                    success: false,
                    deadlineUtc: DateTime.UtcNow.AddMilliseconds(-1),
                    response: null,
                    exception: new TaskCanceledException("The request timed out.", new TimeoutException()));
            }

            return new ExportClientHttpResponse(success: true, deadlineUtc: deadlineUtc, response: null, exception: null);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

    /// <summary>
    /// Simulates a server that returns a fast no-status failure (with remaining deadline budget)
    /// on the first request and succeeds on the second. Used to verify that retries happen when
    /// there is still time remaining in the overall batch deadline.
    /// </summary>
    private sealed class EarlyFailureThenSuccessExportClient : IExportClient
    {
        public int SendCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            this.SendCount++;

            if (this.SendCount == 1)
            {
                // Simulate a fast no-status failure (e.g. connection refused) with time still
                // remaining in the overall deadline.
                return new ExportClientHttpResponse(
                    success: false,
                    deadlineUtc: deadlineUtc,
                    response: null,
                    exception: new HttpRequestException("Connection refused."));
            }

            return new ExportClientHttpResponse(success: true, deadlineUtc: deadlineUtc, response: null, exception: null);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

    /// <summary>
    /// Simulates a server that always returns a no-status failure with an already-expired deadline.
    /// </summary>
    private sealed class PersistentTimeoutExportClient : IExportClient
    {
        public int SendCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            this.SendCount++;

            return new ExportClientHttpResponse(
                success: false,
                deadlineUtc: DateTime.UtcNow.AddMilliseconds(-1),
                response: null,
                exception: new TaskCanceledException("The request timed out.", new TimeoutException()));
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

#if NET
    private sealed class AllocationAssertion : IDisposable
    {
        private readonly long before = GC.GetAllocatedBytesForCurrentThread();

        public void Dispose()
        {
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - this.before;

            const int Limit = 2_000_000;
            Assert.False(
                allocatedBytes > Limit,
                $"{allocatedBytes} bytes were allocated during the operation which is more than the limit of {Limit}.");
        }
    }
#endif
}
