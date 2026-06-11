// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission.Tests;

public class OtlpExporterRetryTransmissionHandlerTests
{
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
