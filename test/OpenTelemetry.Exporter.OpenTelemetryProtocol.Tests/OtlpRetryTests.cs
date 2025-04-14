// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

public class OtlpRetryTests
{
    public static TheoryData<GrpcRetryTestCase> GrpcRetryTestData => GrpcRetryTestCase.GetGrpcTestCases();

    public static TheoryData<HttpRetryTestCase> HttpRetryTestData => HttpRetryTestCase.GetHttpTestCases();

    [Theory]
    [MemberData(nameof(GrpcRetryTestData))]
    public void TryGetGrpcRetryResultTest(GrpcRetryTestCase testCase)
    {
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
                Assert.Equal(GrpcStatusDeserializer.TryGetGrpcRetryDelay(retryAttempt.ThrottleDelay), retryResult.RetryDelay);
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

            if (retryResult.Throttled)
            {
                Assert.Equal(retryAttempt.ThrottleDelay, retryResult.RetryDelay);
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

    public class GrpcRetryTestCase
    {
        private string testRunnerName;

        private GrpcRetryTestCase(string testRunnerName, GrpcRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
        {
            this.ExpectedRetryAttempts = expectedRetryAttempts;
            this.RetryAttempts = retryAttempts;
            this.testRunnerName = testRunnerName;
        }

        public int ExpectedRetryAttempts { get; }

        public GrpcRetryAttempt[] RetryAttempts { get; }

        public static TheoryData<GrpcRetryTestCase> GetGrpcTestCases()
        {
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

        public struct GrpcRetryAttempt
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

    public class HttpRetryTestCase
    {
        private string testRunnerName;

        private HttpRetryTestCase(string testRunnerName, HttpRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
        {
            this.ExpectedRetryAttempts = expectedRetryAttempts;
            this.RetryAttempts = retryAttempts;
            this.testRunnerName = testRunnerName;
        }

        public int ExpectedRetryAttempts { get; }

        internal HttpRetryAttempt[] RetryAttempts { get; }

        public static TheoryData<HttpRetryTestCase> GetHttpTestCases()
        {
            return
            [
                new("NetworkError", [new(statusCode: null)]),
                new("GatewayTimeout", [new(statusCode: HttpStatusCode.GatewayTimeout, throttleDelay: TimeSpan.FromSeconds(1))]),
#if NETSTANDARD2_1_OR_GREATER || NET
                new("ServiceUnavailable", [new(statusCode: HttpStatusCode.TooManyRequests, throttleDelay: TimeSpan.FromSeconds(1))]),
#endif

                new(
                    "Exponential Backoff",
                    [
                        new(statusCode: null, expectedNextRetryDelayMilliseconds: 1500),
                        new(statusCode: null, expectedNextRetryDelayMilliseconds: 2250),
                        new(statusCode: null, expectedNextRetryDelayMilliseconds: 3375),
                        new(statusCode: null, expectedNextRetryDelayMilliseconds: 5000),
                        new(statusCode: null, expectedNextRetryDelayMilliseconds: 5000)
                    ],
                    expectedRetryAttempts: 5),
                new(
                    "Retry until non-retryable status code encountered",
                    [
                        new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 1500),
                        new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 2250),
                        new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 3375),
                        new(statusCode: HttpStatusCode.BadRequest, expectedSuccess: false),
                        new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 5000)
                    ],
                    expectedRetryAttempts: 4),
                new(
                    "Expired deadline",
                    [
                        new(statusCode: HttpStatusCode.ServiceUnavailable, isDeadlineExceeded: true, expectedSuccess: false)
                    ]),
            ];

            // TODO: Add more cases.
        }

        public override string ToString()
        {
            return this.testRunnerName;
        }

        internal sealed class HttpRetryAttempt
        {
            public ExportClientHttpResponse Response;
            public TimeSpan? ThrottleDelay;
            public int? ExpectedNextRetryDelayMilliseconds;
            public bool ExpectedSuccess;

            internal HttpRetryAttempt(
                HttpStatusCode? statusCode,
                TimeSpan? throttleDelay = null,
                bool isDeadlineExceeded = false,
                int expectedNextRetryDelayMilliseconds = 1500,
                bool expectedSuccess = true)
            {
                this.ThrottleDelay = throttleDelay;

                HttpResponseMessage? responseMessage = null;
                if (statusCode != null)
                {
                    responseMessage = new HttpResponseMessage();

                    if (throttleDelay != null)
                    {
                        responseMessage.Headers.RetryAfter = new RetryConditionHeaderValue(throttleDelay.Value);
                    }

                    responseMessage.StatusCode = (HttpStatusCode)statusCode;
                }

                // Using arbitrary +1 hr for deadline for test purposes.
                var deadlineUtc = isDeadlineExceeded ? DateTime.UtcNow.AddMilliseconds(-1) : DateTime.UtcNow.AddHours(1);
                this.Response = new ExportClientHttpResponse(expectedSuccess, deadlineUtc, responseMessage, new HttpRequestException());
                this.ExpectedNextRetryDelayMilliseconds = expectedNextRetryDelayMilliseconds;
                this.ExpectedSuccess = expectedSuccess;
            }
        }
    }
}
