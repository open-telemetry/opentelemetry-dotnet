// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

public class OtlpRetryTests
{
    public static IEnumerable<object[]> GrpcRetryTestData => GrpcRetryTestCase.GetTestCases();

    [Theory]
    [MemberData(nameof(GrpcRetryTestData))]
    public void TryGetGrpcRetryResultTest(GrpcRetryTestCase testCase)
    {
        var attempts = 0;
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;

        foreach (var retryAttempt in testCase.RetryAttempts)
        {
            ++attempts;
            var statusCode = retryAttempt.RpcException.StatusCode;
            var deadline = retryAttempt.CallOptions.Deadline;
            var trailers = retryAttempt.RpcException.Trailers;
            var success = OtlpRetry.TryGetGrpcRetryResult(statusCode, deadline, trailers, nextRetryDelayMilliseconds, out var retryResult);

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
        public int ExpectedRetryAttempts;
        public GrpcRetryAttempt[] RetryAttempts;

        private string testRunnerName;

        private GrpcRetryTestCase(string testRunnerName, GrpcRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
        {
            this.ExpectedRetryAttempts = expectedRetryAttempts;
            this.RetryAttempts = retryAttempts;
            this.testRunnerName = testRunnerName;
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new[] { new GrpcRetryTestCase("Cancelled", new GrpcRetryAttempt[] { new(StatusCode.Cancelled) }) };
            yield return new[] { new GrpcRetryTestCase("DeadlineExceeded", new GrpcRetryAttempt[] { new(StatusCode.DeadlineExceeded) }) };
            yield return new[] { new GrpcRetryTestCase("Aborted", new GrpcRetryAttempt[] { new(StatusCode.Aborted) }) };
            yield return new[] { new GrpcRetryTestCase("OutOfRange", new GrpcRetryAttempt[] { new(StatusCode.OutOfRange) }) };
            yield return new[] { new GrpcRetryTestCase("DataLoss", new GrpcRetryAttempt[] { new(StatusCode.DataLoss) }) };
            yield return new[] { new GrpcRetryTestCase("Unavailable", new GrpcRetryAttempt[] { new(StatusCode.Unavailable) }) };

            yield return new[] { new GrpcRetryTestCase("OK", new GrpcRetryAttempt[] { new(StatusCode.OK, expectedSuccess: false) }) };
            yield return new[] { new GrpcRetryTestCase("PermissionDenied", new GrpcRetryAttempt[] { new(StatusCode.PermissionDenied, expectedSuccess: false) }) };
            yield return new[] { new GrpcRetryTestCase("Unknown", new GrpcRetryAttempt[] { new(StatusCode.Unknown, expectedSuccess: false) }) };

            yield return new[] { new GrpcRetryTestCase("ResourceExhausted w/o RetryInfo", new GrpcRetryAttempt[] { new(StatusCode.ResourceExhausted, expectedSuccess: false) }) };
            yield return new[] { new GrpcRetryTestCase("ResourceExhausted w/ RetryInfo", new GrpcRetryAttempt[] { new(StatusCode.ResourceExhausted, throttleDelay: new Duration { Seconds = 2 }, expectedNextRetryDelayMilliseconds: 3000) }) };

            yield return new[] { new GrpcRetryTestCase("Unavailable w/ RetryInfo", new GrpcRetryAttempt[] { new(StatusCode.Unavailable, throttleDelay: Duration.FromTimeSpan(TimeSpan.FromMilliseconds(2000)), expectedNextRetryDelayMilliseconds: 3000) }) };

            yield return new[] { new GrpcRetryTestCase("Expired deadline", new GrpcRetryAttempt[] { new(StatusCode.Unavailable, deadlineExceeded: true, expectedSuccess: false) }) };

            yield return new[]
            {
                new GrpcRetryTestCase(
                    "Exponential backoff",
                    new GrpcRetryAttempt[]
                    {
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                    },
                    expectedRetryAttempts: 5),
            };

            yield return new[]
            {
                new GrpcRetryTestCase(
                    "Retry until non-retryable status code encountered",
                    new GrpcRetryAttempt[]
                    {
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3375),
                        new(StatusCode.PermissionDenied, expectedSuccess: false),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                    },
                    expectedRetryAttempts: 4),
            };

            // Test throttling affects exponential backoff.
            yield return new[]
            {
                new GrpcRetryTestCase(
                    "Exponential backoff after throttling",
                    new GrpcRetryAttempt[]
                    {
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1500),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2250),
                        new(StatusCode.Unavailable, throttleDelay: Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)), expectedNextRetryDelayMilliseconds: 750),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1125),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 1688),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 2532),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 3798),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                        new(StatusCode.Unavailable, expectedNextRetryDelayMilliseconds: 5000),
                    },
                    expectedRetryAttempts: 9),
            };

            yield return new[]
            {
                new GrpcRetryTestCase(
                    "Ridiculous throttling delay",
                    new GrpcRetryAttempt[]
                    {
                        new(StatusCode.Unavailable, throttleDelay: Duration.FromTimeSpan(TimeSpan.FromDays(3000000)), expectedNextRetryDelayMilliseconds: 5000),
                    }),
            };
        }

        public override string ToString()
        {
            return this.testRunnerName;
        }

        private static Metadata GenerateTrailers(Duration throttleDelay)
        {
            var metadata = new Metadata();

            var retryInfo = new Google.Rpc.RetryInfo();
            retryInfo.RetryDelay = throttleDelay;

            var status = new Google.Rpc.Status();
            status.Details.Add(Any.Pack(retryInfo));

            var stream = new MemoryStream();
            status.WriteTo(stream);

            metadata.Add(OtlpRetry.GrpcStatusDetailsHeader, stream.ToArray());
            return metadata;
        }

        public struct GrpcRetryAttempt
        {
            public RpcException RpcException;
            public CallOptions CallOptions;
            public TimeSpan? ThrottleDelay;
            public int? ExpectedNextRetryDelayMilliseconds;
            public bool ExpectedSuccess;

            public GrpcRetryAttempt(
                StatusCode statusCode,
                bool deadlineExceeded = false,
                Duration throttleDelay = null,
                int expectedNextRetryDelayMilliseconds = 1500,
                bool expectedSuccess = true)
            {
                var status = new Status(statusCode, "Error");
                this.RpcException = throttleDelay != null
                    ? new RpcException(status, GenerateTrailers(throttleDelay))
                    : new RpcException(status);

                this.CallOptions = deadlineExceeded ? new CallOptions(deadline: DateTime.UtcNow.AddSeconds(-1)) : default;

                this.ThrottleDelay = throttleDelay != null ? throttleDelay.ToTimeSpan() : null;

                this.ExpectedNextRetryDelayMilliseconds = expectedNextRetryDelayMilliseconds;

                this.ExpectedSuccess = expectedSuccess;
            }
        }
    }
}
