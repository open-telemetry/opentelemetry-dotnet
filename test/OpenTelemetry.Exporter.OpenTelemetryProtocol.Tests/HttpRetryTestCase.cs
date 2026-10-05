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
    private static readonly TimeSpan MinThrottleDelay = TimeSpan.FromMilliseconds(100);

    private readonly string testRunnerName;

    private HttpRetryTestCase(string testRunnerName, HttpRetryAttempt[] retryAttempts, int expectedRetryAttempts = 1)
    {
        this.ExpectedRetryAttempts = expectedRetryAttempts;
        this.RetryAttempts = retryAttempts;
        this.testRunnerName = testRunnerName;
    }

    public int ExpectedRetryAttempts { get; }

    internal HttpRetryAttempt[] RetryAttempts { get; }

    public static TheoryData<HttpRetryTestCase> GetHttpTestCases() =>
    [
        new HttpRetryTestCase("NetworkError", [new(statusCode: null)]),
        new HttpRetryTestCase("NetworkError with expired deadline", [new(statusCode: null, isDeadlineExceeded: true, expectedSuccess: false)]),
#if NET
        new HttpRetryTestCase("Unknown HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.Unknown))]),
        new HttpRetryTestCase("NameResolutionError HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.NameResolutionError))]),
        new HttpRetryTestCase("ConnectionError HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.ConnectionError))]),
        new HttpRetryTestCase("SecureConnectionError HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.SecureConnectionError))]),
        new HttpRetryTestCase("HttpProtocolError HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.HttpProtocolError))]),
        new HttpRetryTestCase("ExtendedConnectNotSupported HttpRequestError", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.ExtendedConnectNotSupported))]),
        new HttpRetryTestCase("VersionNegotiationError HttpRequestError", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.VersionNegotiationError))]),
        new HttpRetryTestCase("UserAuthenticationError HttpRequestError", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.UserAuthenticationError))]),
        new HttpRetryTestCase("ProxyTunnelError HttpRequestError without status code", [new(statusCode: null, httpRequestException: new(HttpRequestError.ProxyTunnelError))]),
        new HttpRetryTestCase("ProxyTunnelError HttpRequestError with ProxyAuthenticationRequired status code", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.ProxyTunnelError, statusCode: HttpStatusCode.ProxyAuthenticationRequired))]),
        new HttpRetryTestCase("ProxyTunnelError HttpRequestError with BadGateway status code", [new(statusCode: null, httpRequestException: new(HttpRequestError.ProxyTunnelError, statusCode: HttpStatusCode.BadGateway))]),
        new HttpRetryTestCase("ProxyTunnelError HttpRequestError with ServiceUnavailable status code", [new(statusCode: null, httpRequestException: new(HttpRequestError.ProxyTunnelError, statusCode: HttpStatusCode.ServiceUnavailable))]),
        new HttpRetryTestCase("InvalidResponse HttpRequestError", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.InvalidResponse))]),
        new HttpRetryTestCase("ResponseEnded HttpRequestError", [new(statusCode: null, httpRequestException: new(HttpRequestError.ResponseEnded))]),
        new HttpRetryTestCase("ConfigurationLimitExceeded HttpRequestError", [new(statusCode: null, expectedSuccess: false, httpRequestException: new(HttpRequestError.ConfigurationLimitExceeded))]),
#endif
        new HttpRetryTestCase("GatewayTimeout", [new(statusCode: HttpStatusCode.GatewayTimeout, throttleDelay: TimeSpan.FromSeconds(1))]),
        new HttpRetryTestCase("ServiceUnavailable", [new(statusCode: HttpStatusCode.ServiceUnavailable, throttleDelay: TimeSpan.FromSeconds(1), expectedThrottled: true)]),

        // A "Retry-After: 0" is clamped to a non-zero minimum
        new HttpRetryTestCase("ServiceUnavailable w/ zero Retry-After", [new(statusCode: HttpStatusCode.ServiceUnavailable, throttleDelay: TimeSpan.Zero, expectedThrottled: true, expectedRetryDelay: MinThrottleDelay, expectedNextRetryDelayMilliseconds: 150)]),

        // A throttle delay that would push the retry past the configured deadline must
        // fail fast and drop the data rather than blocking for the throttle duration.
        new HttpRetryTestCase("ServiceUnavailable (Delta) exceeds deadline", [new(statusCode: HttpStatusCode.ServiceUnavailable, throttleDelay: TimeSpan.FromSeconds(30), deadlineFromNow: TimeSpan.FromSeconds(1), expectedSuccess: false)]),
        new HttpRetryTestCase("ServiceUnavailable (HTTP-Date) exceeds deadline", [new(statusCode: HttpStatusCode.ServiceUnavailable, throttleDelay: TimeSpan.FromSeconds(30), deadlineFromNow: TimeSpan.FromSeconds(1), expectedSuccess: false, useDateForRetryCondition: true)]),

#if NET
        new HttpRetryTestCase("TooManyRequests (Delta)", [new(statusCode: HttpStatusCode.TooManyRequests, throttleDelay: TimeSpan.FromSeconds(1), expectedThrottled: true)]),
        new HttpRetryTestCase("TooManyRequests (HTTP-Date)", [new(statusCode: HttpStatusCode.TooManyRequests, throttleDelay: TimeSpan.FromSeconds(1), expectedThrottled: true, useDateForRetryCondition: true)]),
        new HttpRetryTestCase("TooManyRequests (Delta) too large", [new(statusCode: HttpStatusCode.TooManyRequests, throttleDelay: TimeSpan.FromSeconds(30), expectedNextRetryDelayMilliseconds: 5000, expectedThrottled: true)]),
        new HttpRetryTestCase("TooManyRequests (HTTP-Date) too large", [new(statusCode: HttpStatusCode.TooManyRequests, throttleDelay: TimeSpan.FromSeconds(30), expectedNextRetryDelayMilliseconds: 5000, expectedThrottled: true, useDateForRetryCondition: true)]),
#else
        new HttpRetryTestCase("TooManyRequests (Delta)", [new(statusCode: (HttpStatusCode)429, throttleDelay: TimeSpan.FromSeconds(1), expectedThrottled: true)]),
        new HttpRetryTestCase("TooManyRequests (HTTP-Date)", [new(statusCode: (HttpStatusCode)429, throttleDelay: TimeSpan.FromSeconds(1), expectedThrottled: true, useDateForRetryCondition: true)]),
        new HttpRetryTestCase("TooManyRequests (Delta) too large", [new(statusCode: (HttpStatusCode)429, throttleDelay: TimeSpan.FromSeconds(30), expectedNextRetryDelayMilliseconds: 5000, expectedThrottled: true)]),
        new HttpRetryTestCase("TooManyRequests (HTTP-Date) too large", [new(statusCode: (HttpStatusCode)429, throttleDelay: TimeSpan.FromSeconds(30), expectedNextRetryDelayMilliseconds: 5000, expectedThrottled: true, useDateForRetryCondition: true)]),
#endif

        new HttpRetryTestCase(
            "Exponential Backoff",
            [
                new(statusCode: null, expectedNextRetryDelayMilliseconds: 1500),
                new(statusCode: null, expectedNextRetryDelayMilliseconds: 2250),
                new(statusCode: null, expectedNextRetryDelayMilliseconds: 3375),
                new(statusCode: null, expectedNextRetryDelayMilliseconds: 5000),
                new(statusCode: null, expectedNextRetryDelayMilliseconds: 5000)
            ],
            expectedRetryAttempts: 5),
        new HttpRetryTestCase(
            "Retry until non-retryable status code encountered",
            [
                new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 1500),
                new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 2250),
                new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 3375),
                new(statusCode: HttpStatusCode.BadRequest, expectedSuccess: false),
                new(statusCode: HttpStatusCode.ServiceUnavailable, expectedNextRetryDelayMilliseconds: 5000)
            ],
            expectedRetryAttempts: 4),
        new HttpRetryTestCase(
            "Expired deadline",
            [
                new(statusCode: HttpStatusCode.ServiceUnavailable, isDeadlineExceeded: true, expectedSuccess: false)
            ]),
    ];

    public override string ToString() => this.testRunnerName;

    internal sealed class HttpRetryAttempt
    {
        public TimeSpan? ThrottleDelay;
        public TimeSpan? ExpectedRetryDelay;
        public TimeSpan TimestampTolerance;
        public int? ExpectedNextRetryDelayMilliseconds;
        public bool ExpectedSuccess;
        public bool ExpectedThrottled;

        private readonly Func<ExportClientHttpResponse> createResponse;

        internal HttpRetryAttempt(
            HttpStatusCode? statusCode,
            TimeSpan? throttleDelay = null,
            bool isDeadlineExceeded = false,
            int expectedNextRetryDelayMilliseconds = 1500,
            bool expectedSuccess = true,
            bool expectedThrottled = false,
            bool useDateForRetryCondition = false,
            TimeSpan? deadlineFromNow = null,
            TimeSpan? expectedRetryDelay = null,
            HttpRequestException? httpRequestException = null)
        {
            this.ThrottleDelay = throttleDelay;
            this.ExpectedRetryDelay = expectedRetryDelay ?? throttleDelay;
            this.TimestampTolerance = useDateForRetryCondition ? TimeSpan.FromMilliseconds(expectedNextRetryDelayMilliseconds) : TimeSpan.Zero;

            this.createResponse = () =>
            {
                HttpResponseMessage? responseMessage = null;
                if (statusCode != null)
                {
#pragma warning disable CA2000 // Dispose objects before losing scope
                    responseMessage = new HttpResponseMessage();
#pragma warning restore CA2000 // Dispose objects before losing scope

                    if (throttleDelay is { } value)
                    {
                        responseMessage.Headers.RetryAfter = useDateForRetryCondition
                            ? new RetryConditionHeaderValue(DateTimeOffset.UtcNow.Add(value))
                            : new RetryConditionHeaderValue(value);
                    }

                    responseMessage.StatusCode = (HttpStatusCode)statusCode;
                }

                // Using arbitrary +1 hr for deadline for test purposes, unless a deadline is specified.
                var deadlineUtc = isDeadlineExceeded
                    ? DateTime.UtcNow.AddMilliseconds(-1)
                    : DateTime.UtcNow.Add(deadlineFromNow ?? TimeSpan.FromHours(1));

                return new ExportClientHttpResponse(expectedSuccess, deadlineUtc, responseMessage, httpRequestException ?? new HttpRequestException());
            };

            this.ExpectedNextRetryDelayMilliseconds = expectedNextRetryDelayMilliseconds;
            this.ExpectedSuccess = expectedSuccess;
            this.ExpectedThrottled = expectedThrottled;
        }

        /// <summary>
        /// Gets the response for this attempt, built the first time it is asked for.
        /// </summary>
        /// <remarks>
        /// A Retry-After carrying an HTTP-date has a resolution of one second, so the
        /// header describes an instant less than a second after it was built and the
        /// delay it asks for has elapsed once that second is out. Every case of a
        /// theory is built together and the cases are then run one at a time, which
        /// takes long enough for that to happen, so the response is built when a test
        /// reaches it rather than when the case is created.
        /// </remarks>
        public ExportClientHttpResponse Response
        {
            get => field ??= this.createResponse();
            private set;
        }
    }
}
