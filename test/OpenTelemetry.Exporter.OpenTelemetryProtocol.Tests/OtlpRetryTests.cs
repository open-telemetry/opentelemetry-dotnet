// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

public class OtlpRetryTests
{
    public static TheoryData<GrpcRetryTestCase> GrpcRetryTestData => GrpcRetryTestCase.GetGrpcTestCases();

    public static TheoryData<HttpRetryTestCase> HttpRetryTestData => HttpRetryTestCase.GetHttpTestCases();

    [Theory]
    [MemberData(nameof(GrpcRetryTestData))]
    public void TryGetGrpcRetryResultTest(GrpcRetryTestCase testCase)
    {
#if NET
        Assert.NotNull(testCase);
#else
        if (testCase == null)
        {
            throw new ArgumentNullException(nameof(testCase));
        }
#endif
        var attempts = 0;
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;

        foreach (var retryAttempt in testCase.RetryAttempts)
        {
            ++attempts;
            Assert.NotNull(retryAttempt.Response.Status);
            var statusCode = retryAttempt.Response.Status.Value.StatusCode;
            var deadline = retryAttempt.Response.DeadlineUtc;
            var trailers = retryAttempt.Response.GrpcStatusDetailsHeader;
            var success = OtlpRetry.TryGetGrpcRetryResult(retryAttempt.Response, nextRetryDelayMilliseconds, out var retryResult);

            Assert.Equal(retryAttempt.ExpectedSuccess, success);

            if (!success)
            {
                Assert.Equal(testCase.ExpectedRetryAttempts, attempts);
                break;
            }

            if (retryResult.Throttled)
            {
                Assert.Equal(retryAttempt.ExpectedRetryDelay, retryResult.RetryDelay);
            }
            else
            {
                Assert.True(retryResult.RetryDelay >= TimeSpan.Zero);
                Assert.True(retryResult.RetryDelay < TimeSpan.FromMilliseconds(nextRetryDelayMilliseconds));
            }

            Assert.Equal(retryAttempt.ExpectedNextRetryDelayMilliseconds, retryResult.NextRetryDelayMilliseconds);

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        Assert.Equal(testCase.ExpectedRetryAttempts, attempts);
    }

    [Theory]
    [MemberData(nameof(HttpRetryTestData))]
    public void TryGetHttpRetryResultTest(HttpRetryTestCase testCase)
    {
#if NET
        Assert.NotNull(testCase);
#else
        if (testCase == null)
        {
            throw new ArgumentNullException(nameof(testCase));
        }
#endif
        var attempts = 0;
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;

        foreach (var retryAttempt in testCase.RetryAttempts)
        {
            ++attempts;
            var statusCode = retryAttempt.Response.StatusCode;
            var deadline = retryAttempt.Response.DeadlineUtc;
            var headers = retryAttempt.Response.Headers;
            var success = OtlpRetry.TryGetHttpRetryResult(retryAttempt.Response, nextRetryDelayMilliseconds, out var retryResult);

            Assert.Equal(retryAttempt.ExpectedSuccess, success);

            if (!success)
            {
                Assert.Equal(testCase.ExpectedRetryAttempts, attempts);
                break;
            }

            Assert.Equal(retryAttempt.ExpectedThrottled, retryResult.Throttled);

            if (retryResult.Throttled)
            {
                Assert.Equal(retryAttempt.ExpectedRetryDelay!.Value.TotalSeconds, retryResult.RetryDelay.TotalSeconds, retryAttempt.TimestampTolerance.TotalSeconds);
            }
            else
            {
                Assert.True(retryResult.RetryDelay >= TimeSpan.Zero);
                Assert.True(retryResult.RetryDelay < TimeSpan.FromMilliseconds(nextRetryDelayMilliseconds));
            }

            Assert.Equal(retryAttempt.ExpectedNextRetryDelayMilliseconds!.Value, retryResult.NextRetryDelayMilliseconds, retryAttempt.TimestampTolerance.TotalMilliseconds);

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        Assert.Equal(testCase.ExpectedRetryAttempts, attempts);
    }

    [Fact]
    public void ZeroThrottleDelayDoesNotCauseAnUnthrottledRetryStorm()
    {
        const double TimeoutMilliseconds = 250;

        var exportClient = new AlwaysThrottledExportClient(
            GrpcRetryTestCase.GetThrottleDelayString(new Google.Protobuf.WellKnownTypes.Duration()));

        using var handler = new OtlpExporterRetryTransmissionHandler(exportClient, TimeoutMilliseconds);

        Assert.False(handler.TrySubmitRequest([1, 2, 3], 3));

        Assert.True(
            exportClient.SendCount <= 10,
            $"Expected the retry rate to be throttled but saw {exportClient.SendCount} attempts in {TimeoutMilliseconds}ms.");
    }

    [Fact]
    public void RetryableStatusWithoutThrottleDelayDoesNotThrowOnAZeroBackoff()
    {
        var response = new ExportClientGrpcResponse(
            success: false,
            deadlineUtc: DateTime.UtcNow.AddHours(1),
            exception: null,
            status: new Status(StatusCode.Unavailable, "Error"),
            grpcStatusDetailsHeader: null);

        Assert.Null(Record.Exception(
            () => OtlpRetry.TryGetGrpcRetryResult(response, retryDelayMilliseconds: 0, out _)));
    }

    private sealed class AlwaysThrottledExportClient(string grpcStatusDetailsHeader) : IExportClient
    {
        public int SendCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            this.SendCount++;

            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: null,
                status: new Status(StatusCode.ResourceExhausted, "Throttled"),
                grpcStatusDetailsHeader: grpcStatusDetailsHeader);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }
}
