// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission.Tests;

public class OtlpExporterRetryTransmissionHandlerTests
{
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
        private readonly long before;

        public AllocationAssertion()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            this.before = GC.GetTotalAllocatedBytes();
        }

        public void Dispose()
        {
            var allocatedBytes = GC.GetTotalAllocatedBytes() - this.before;

            const int Limit = 2_000_000;
            Assert.False(
                allocatedBytes > Limit,
                $"{allocatedBytes} bytes were allocated during the operation which is more than the limit of {Limit}.");
        }
    }
#endif
}
