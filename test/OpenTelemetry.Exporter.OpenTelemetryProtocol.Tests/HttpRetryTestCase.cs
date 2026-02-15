// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public class HttpRetryTestCase
#pragma warning restore CA1515 // Consider making public types internal
{
    private readonly string testRunnerName;

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
#pragma warning disable CA1825 // Workaround false positive in .NET 11
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
#pragma warning restore CA1825

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
#pragma warning disable CA2000 // Dispose objects before losing scope
                responseMessage = new HttpResponseMessage();
#pragma warning restore CA2000 // Dispose objects before losing scope

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
