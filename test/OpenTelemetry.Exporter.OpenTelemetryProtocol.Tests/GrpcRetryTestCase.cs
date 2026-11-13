// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

internal sealed class GrpcRetryTestCase
{
    private static readonly TimeSpan MinThrottleDelay = TimeSpan.FromMilliseconds(100);

    private readonly string testRunnerName;

    private GrpcRetryTestCase(string testRunnerName, GrpcRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
    {
        this.ExpectedRetryAttempts = expectedRetryAttempts;
        this.RetryAttempts = retryAttempts;
        this.testRunnerName = testRunnerName;
    }

    public int ExpectedRetryAttempts { get; }

    internal GrpcRetryAttempt[] RetryAttempts { get; }

    public static TheoryData<string> GetGrpcTestCases()
        => [.. CreateTestCases().Select(testCase => testCase.testRunnerName)];

    public static GrpcRetryTestCase GetTestCase(string name)
        => CreateTestCases().Single(testCase => string.Equals(testCase.testRunnerName, name, StringComparison.Ordinal));

    public override string ToString()
        => this.testRunnerName;

    internal static string GetThrottleDelayString(Duration throttleDelay, StatusCode statusCode)
    {
        var status = new Google.Rpc.Status
        {
            Code = (int)statusCode,
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

    private static GrpcRetryTestCase[] CreateTestCases() =>
    [
        new GrpcRetryTestCase("Cancelled", [new(StatusCode.Cancelled)]),
        new GrpcRetryTestCase("DeadlineExceeded", [new(StatusCode.DeadlineExceeded)]),
        new GrpcRetryTestCase("Aborted", [new(StatusCode.Aborted)]),
        new GrpcRetryTestCase("OutOfRange", [new(StatusCode.OutOfRange)]),
        new GrpcRetryTestCase("DataLoss", [new(StatusCode.DataLoss)]),
        new GrpcRetryTestCase("Unavailable", [new(StatusCode.Unavailable)]),

        new GrpcRetryTestCase("OK", [new(StatusCode.OK, expectedSuccess: false)]),
        new GrpcRetryTestCase("PermissionDenied", [new(StatusCode.PermissionDenied, expectedSuccess: false)]),
        new GrpcRetryTestCase("Unknown", [new(StatusCode.Unknown, expectedSuccess: false)]),

        new GrpcRetryTestCase("ResourceExhausted w/o RetryInfo", [new(StatusCode.ResourceExhausted, expectedSuccess: false)]),
        new GrpcRetryTestCase("ResourceExhausted w/ RetryInfo", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(new Duration { Seconds = 2 }, StatusCode.ResourceExhausted), expectedNextRetryDelayMilliseconds: 3000)]),
        new GrpcRetryTestCase("ResourceExhausted w/ zero RetryInfo", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(new Duration(), StatusCode.ResourceExhausted), expectedRetryDelay: MinThrottleDelay, expectedNextRetryDelayMilliseconds: 150)]),
        new GrpcRetryTestCase("ResourceExhausted w/ sub-millisecond RetryInfo", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(new Duration { Nanos = 1 }, StatusCode.ResourceExhausted), expectedRetryDelay: MinThrottleDelay, expectedNextRetryDelayMilliseconds: 150)]),

        new GrpcRetryTestCase("Unavailable w/ RetryInfo", [new(StatusCode.Unavailable, throttleDelay: GetThrottleDelayString(Duration.FromTimeSpan(TimeSpan.FromMilliseconds(2000)), StatusCode.Unavailable), expectedNextRetryDelayMilliseconds: 3000)]),

        // Status details which contradict the status of the response are ignored, so there is no throttle delay to honor.
        new GrpcRetryTestCase("ResourceExhausted w/ RetryInfo for a different status", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(new Duration { Seconds = 2 }, StatusCode.InvalidArgument), expectedSuccess: false)]),
        new GrpcRetryTestCase("Unavailable w/ RetryInfo for a different status", [new(StatusCode.Unavailable, throttleDelay: GetThrottleDelayString(new Duration { Seconds = 2 }, StatusCode.InvalidArgument), expectedNextRetryDelayMilliseconds: 1500)]),

        new GrpcRetryTestCase("Expired deadline", [new(StatusCode.Unavailable, deadlineExceeded: true, expectedSuccess: false)]),

        // A throttle delay that would push the retry past the configured deadline must
        // fail fast and drop the data rather than blocking for the throttle duration.
        new GrpcRetryTestCase("Throttle delay exceeds deadline", [new(StatusCode.ResourceExhausted, throttleDelay: GetThrottleDelayString(Duration.FromTimeSpan(TimeSpan.FromSeconds(30)), StatusCode.ResourceExhausted), deadlineFromNow: TimeSpan.FromSeconds(1), expectedSuccess: false)]),

        new GrpcRetryTestCase(
            "Exponential backoff",
            [
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
            ],
            expectedRetryAttempts: 5),

        new GrpcRetryTestCase(
            "Retry until non-retryable status code encountered",
            [
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                new(StatusCode.PermissionDenied, expectedSuccess: false),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
            ],
            expectedRetryAttempts: 4),

        // Test throttling affects exponential backoff.
        new GrpcRetryTestCase(
            "Exponential backoff after throttling",
            [
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                new(StatusCode.Unavailable, throttleDelay: GetThrottleDelayString(Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)), StatusCode.Unavailable), expectedNextRetryDelayMilliseconds: 750),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1125),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1688),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2532),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3798),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
            ],
            expectedRetryAttempts: 9),
    ];

    internal readonly struct GrpcRetryAttempt
    {
        internal GrpcRetryAttempt(
            StatusCode statusCode,
            bool deadlineExceeded = false,
            string? throttleDelay = null,
            int expectedNextRetryDelayMilliseconds = 1500,
            bool expectedSuccess = true,
            TimeSpan? deadlineFromNow = null,
            TimeSpan? expectedRetryDelay = null)
        {
            var status = new Status(statusCode, "Error");

            // Using arbitrary +1 hr for deadline for test purposes, unless a deadline is specified.
            var deadlineUtc = deadlineExceeded
                ? DateTime.UtcNow.AddSeconds(-1)
                : DateTime.UtcNow.Add(deadlineFromNow ?? TimeSpan.FromHours(1));

            this.ThrottleDelay = throttleDelay;

            this.Response = new ExportClientGrpcResponse(expectedSuccess, deadlineUtc, null, status, this.ThrottleDelay);

            this.ExpectedNextRetryDelayMilliseconds = expectedNextRetryDelayMilliseconds;

            this.ExpectedRetryDelay = expectedRetryDelay ?? GrpcStatusDeserializer.TryGetGrpcRetryDelay(throttleDelay);

            this.ExpectedSuccess = expectedSuccess;
        }

        public string? ThrottleDelay { get; }

        public TimeSpan? ExpectedRetryDelay { get; }

        public int? ExpectedNextRetryDelayMilliseconds { get; }

        public bool ExpectedSuccess { get; }

        internal ExportClientGrpcResponse Response { get; }
    }
}
