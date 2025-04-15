// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
            var success = OtlpRetry.TryGetGrpcRetryResult(retryAttempt.Response, nextRetryDelayMilliseconds,
                out var retryResult);

            Assert.Equal(retryAttempt.ExpectedSuccess, success);

            if (!success)
            {
                Assert.Equal(testCase.ExpectedRetryAttempts, attempts);
                break;
            }

            if (retryResult.Throttled)
            {
                Assert.Equal(GrpcStatusDeserializer.TryGetGrpcRetryDelay(retryAttempt.ThrottleDelay),
                    retryResult.RetryDelay);
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
            var success = OtlpRetry.TryGetHttpRetryResult(retryAttempt.Response, nextRetryDelayMilliseconds,
                out var retryResult);

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
}
