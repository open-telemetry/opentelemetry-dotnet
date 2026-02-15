// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public class GrpcRetryTestCase
#pragma warning restore CA1515 // Consider making public types internal
{
    private readonly string testRunnerName;

    private GrpcRetryTestCase(string testRunnerName, GrpcRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
    {
        this.ExpectedRetryAttempts = expectedRetryAttempts;
        this.RetryAttempts = retryAttempts;
        this.testRunnerName = testRunnerName;
    }

    public int ExpectedRetryAttempts { get; }

    internal GrpcRetryAttempt[] RetryAttempts { get; }

    public static TheoryData<GrpcRetryTestCase> GetGrpcTestCases()
    {
#pragma warning disable CA1825 // HACK Workaround for https://github.com/dotnet/sdk/issues/53047
        return
        [
            new("Cancelled", [new(StatusCode.Cancelled)]),
            new("DeadlineExceeded", [new(StatusCode.DeadlineExceeded)]),
            new("Aborted", [new(StatusCode.Aborted)]),
            new("OutOfRange", [new(StatusCode.OutOfRange)]),
            new("DataLoss", [new(StatusCode.DataLoss)]),
            new("Unavailable", [new(StatusCode.Unavailable)]),

            new("OK", [new(StatusCode.OK, expectedSuccess: false)]),
            new("PermissionDenied", [new(StatusCode.PermissionDenied, expectedSuccess: false)]),
            new("Unknown", [new(StatusCode.Unknown, expectedSuccess: false)]),

            new("ResourceExhausted w/o RetryInfo", [new(StatusCode.ResourceExhausted, expectedSuccess: false)]),
            new("ResourceExhausted w/ RetryInfo", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(new Duration { Seconds = 2 }), expectedNextRetryDelayMilliseconds: 3000)]),

            new("Unavailable w/ RetryInfo", [new(StatusCode.Unavailable, throttleDelay: GetThrottleDelayString(Duration.FromTimeSpan(TimeSpan.FromMilliseconds(2000))), expectedNextRetryDelayMilliseconds: 3000)]),

            new("Expired deadline", [new(StatusCode.Unavailable, deadlineExceeded: true, expectedSuccess: false)]),

            new(
                "Exponential backoff",
                [
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000)
                ],
                expectedRetryAttempts: 5),

            new(
                "Retry until non-retryable status code encountered",
                [
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                    new(StatusCode.PermissionDenied, expectedSuccess: false),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000)
                ],
                expectedRetryAttempts: 4),

            // Test throttling affects exponential backoff.
            new(
                "Exponential backoff after throttling",
                [
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                    new(StatusCode.Unavailable, throttleDelay: GetThrottleDelayString(Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500))), expectedNextRetryDelayMilliseconds: 750),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1125),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1688),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2532),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3798),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                    new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000)
                ],
                expectedRetryAttempts: 9),
        ];
#pragma warning restore CA1825
    }

    public override string ToString()
    {
        return this.testRunnerName;
    }

    private static string GetThrottleDelayString(Duration throttleDelay)
    {
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Only nanos",
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = throttleDelay,
                }),
            },
        };

        return Convert.ToBase64String(status.ToByteArray());
    }

    internal struct GrpcRetryAttempt
    {
        internal GrpcRetryAttempt(
            StatusCode statusCode,
            bool deadlineExceeded = false,
            string? throttleDelay = null,
            int expectedNextRetryDelayMilliseconds = 1500,
            bool expectedSuccess = true)
        {
            var status = new Status(statusCode, "Error");

            // Using arbitrary +1 hr for deadline for test purposes.
            var deadlineUtc = deadlineExceeded ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddHours(1);

            this.ThrottleDelay = throttleDelay;

            this.Response = new ExportClientGrpcResponse(expectedSuccess, deadlineUtc, null, status, this.ThrottleDelay);

            this.ExpectedNextRetryDelayMilliseconds = expectedNextRetryDelayMilliseconds;

            this.ExpectedSuccess = expectedSuccess;
        }

        public string? ThrottleDelay { get; }

        public int? ExpectedNextRetryDelayMilliseconds { get; }

        public bool ExpectedSuccess { get; }

        internal ExportClientGrpcResponse Response { get; }
    }
}
