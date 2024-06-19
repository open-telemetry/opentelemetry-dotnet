// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Google.Rpc;
using Grpc.Core;
using Status = Google.Rpc.Status;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

/// <summary>
/// Implementation of the OTLP retry policy used by both OTLP/gRPC and OTLP/HTTP.
///
/// OTLP/gRPC
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#failures
///
/// OTLP/HTTP
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#failures-1
///
/// The specification requires retries use an exponential backoff strategy,
/// but does not provide specifics for the implementation. As such, this
/// implementation is inspired by the retry strategy provided by
/// Grpc.Net.Client which implements the gRPC retry specification.
///
/// Grpc.Net.Client retry implementation
/// https://github.com/grpc/grpc-dotnet/blob/83d12ea1cb628156c990243bc98699829b88738b/src/Grpc.Net.Client/Internal/Retry/RetryCall.cs#L94
///
/// gRPC retry specification
/// https://github.com/grpc/proposal/blob/master/A6-client-retries.md
///
/// The gRPC retry specification outlines configurable parameters used in its
/// exponential backoff strategy: initial backoff, max backoff, backoff
/// multiplier, and max retry attempts. The OTLP specification does not declare
/// a similar set of parameters, so this implementation uses fixed settings.
/// Furthermore, since the OTLP spec does not specify a max number of attempts,
/// this implementation will retry until the deadline is reached.
///
/// The throttling mechanism for OTLP differs from the throttling mechanism
/// described in the gRPC retry specification. See:
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#otlpgrpc-throttling.
/// </summary>
internal static class OtlpRetry
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin";
    public const int InitialBackoffMilliseconds = 1000;
    private const int MaxBackoffMilliseconds = 5000;
    private const double BackoffMultiplier = 1.5;

#if !NET6_0_OR_GREATER
    private static readonly Random Random = new Random();
#endif

    public static bool TryGetHttpRetryResult(ExportClientHttpResponse response, int retryDelayInMilliSeconds, out RetryResult retryResult)
    {
        if (response.StatusCode.HasValue)
        {
            return TryGetRetryResult(response.StatusCode.Value, IsHttpStatusCodeRetryable, response.DeadlineUtc, response.Headers, TryGetHttpRetryDelay, retryDelayInMilliSeconds, out retryResult);
        }
        else
        {
            if (ShouldHandleHttpRequestException(response.Exception))
            {
                var delay = TimeSpan.FromMilliseconds(GetRandomNumber(0, retryDelayInMilliSeconds));
                if (!IsDeadlineExceeded(response.DeadlineUtc + delay))
                {
                    retryResult = new RetryResult(false, delay, CalculateNextRetryDelay(retryDelayInMilliSeconds));
                    return true;
                }
            }

            retryResult = default;
            return false;
        }
    }

    public static bool ShouldHandleHttpRequestException(Exception? exception)
    {
        // TODO: Handle specific exceptions.
        return true;
    }

    public static bool TryGetGrpcRetryResult(ExportClientGrpcResponse response, int retryDelayMilliseconds, out RetryResult retryResult)
    {
        if (response.Exception is RpcException rpcException)
        {
            return TryGetRetryResult(rpcException.StatusCode, IsGrpcStatusCodeRetryable, response.DeadlineUtc, rpcException.Trailers, TryGetGrpcRetryDelay, retryDelayMilliseconds, out retryResult);
        }

        retryResult = default;
        return false;
    }

    private static bool TryGetRetryResult<TStatusCode, TCarrier>(TStatusCode statusCode, Func<TStatusCode, bool, bool> isRetryable, DateTime? deadline, TCarrier carrier, Func<TStatusCode, TCarrier, TimeSpan?> throttleGetter, int nextRetryDelayMilliseconds, out RetryResult retryResult)
    {
        retryResult = default;

        // TODO: Consider introducing a fixed max number of retries (e.g. max 5 retries).
        // The spec does not specify a max number of retries, but it may be bad to not cap the number of attempts.
        // Without a max number of retry attempts, retries would continue until the deadline.
        // Maybe this is ok? However, it may lead to an unexpected behavioral change. For example:
        //    1) When using a batch processor, a longer delay due to repeated
        //       retries up to the deadline may lead to a higher chance that the queue will be exhausted.
        //    2) When using the simple processor, a longer delay due to repeated
        //       retries up to the deadline will lead to a prolonged blocking call.
        // if (attemptCount >= MaxAttempts)
        // {
        //     return false
        // }

        if (IsDeadlineExceeded(deadline))
        {
            return false;
        }

        var throttleDelay = throttleGetter(statusCode, carrier);
        var retryable = isRetryable(statusCode, throttleDelay.HasValue);
        if (!retryable)
        {
            return false;
        }

        var delayDuration = throttleDelay.HasValue
            ? throttleDelay.Value
            : TimeSpan.FromMilliseconds(GetRandomNumber(0, nextRetryDelayMilliseconds));

        if (deadline.HasValue && IsDeadlineExceeded(deadline + delayDuration))
        {
            return false;
        }

        if (throttleDelay.HasValue)
        {
            try
            {
                // TODO: Consider making nextRetryDelayMilliseconds a double to avoid the need for convert/overflow handling
                nextRetryDelayMilliseconds = Convert.ToInt32(throttleDelay.Value.TotalMilliseconds);
            }
            catch (OverflowException)
            {
                nextRetryDelayMilliseconds = MaxBackoffMilliseconds;
            }
        }

        nextRetryDelayMilliseconds = CalculateNextRetryDelay(nextRetryDelayMilliseconds);
        retryResult = new RetryResult(throttleDelay.HasValue, delayDuration, nextRetryDelayMilliseconds);
        return true;
    }

    private static bool IsDeadlineExceeded(DateTime? deadline)
    {
        // This implementation is internal, and it is guaranteed that deadline is UTC.
        return deadline.HasValue && deadline <= DateTime.UtcNow;
    }

    private static int CalculateNextRetryDelay(int nextRetryDelayMilliseconds)
    {
        var nextMilliseconds = nextRetryDelayMilliseconds * BackoffMultiplier;
        nextMilliseconds = Math.Min(nextMilliseconds, MaxBackoffMilliseconds);
        return Convert.ToInt32(nextMilliseconds);
    }

    private static TimeSpan? TryGetGrpcRetryDelay(StatusCode statusCode, Metadata trailers)
    {
        Debug.Assert(trailers != null, "trailers was null");

        if (statusCode != StatusCode.ResourceExhausted && statusCode != StatusCode.Unavailable)
        {
            return null;
        }

        var statusDetails = trailers!.Get(GrpcStatusDetailsHeader);
        if (statusDetails != null && statusDetails.IsBinary)
        {
            // TODO: Remove google.protobuf dependency for de-serialization.
            var status = Status.Parser.ParseFrom(statusDetails.ValueBytes);
            foreach (var item in status.Details)
            {
                var success = item.TryUnpack<RetryInfo>(out var retryInfo);
                if (success)
                {
                    return retryInfo.RetryDelay.ToTimeSpan();
                }
            }
        }

        return null;
    }

    private static TimeSpan? TryGetHttpRetryDelay(HttpStatusCode statusCode, HttpResponseHeaders? responseHeaders)
    {
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        return statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable
#else
        return statusCode == (HttpStatusCode)429 || statusCode == HttpStatusCode.ServiceUnavailable
#endif
            ? responseHeaders?.RetryAfter?.Delta
            : null;
    }

    private static bool IsGrpcStatusCodeRetryable(StatusCode statusCode, bool hasRetryDelay)
    {
        switch (statusCode)
        {
            case StatusCode.Cancelled:
            case StatusCode.DeadlineExceeded:
            case StatusCode.Aborted:
            case StatusCode.OutOfRange:
            case StatusCode.Unavailable:
            case StatusCode.DataLoss:
                return true;
            case StatusCode.ResourceExhausted:
                return hasRetryDelay;
            default:
                return false;
        }
    }

    private static bool IsHttpStatusCodeRetryable(HttpStatusCode statusCode, bool hasRetryDelay)
    {
        switch (statusCode)
        {
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            case HttpStatusCode.TooManyRequests:
#else
            case (HttpStatusCode)429:
#endif
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                return true;
            default:
                return false;
        }
    }

    private static int GetRandomNumber(int min, int max)
    {
#if NET6_0_OR_GREATER
        return Random.Shared.Next(min, max);
#else
        // TODO: Implement this better to minimize lock contention.
        // Consider pulling in Random.Shared implementation.
        lock (Random)
        {
            return Random.Next(min, max);
        }
#endif
    }

    public readonly struct RetryResult
    {
        public readonly bool Throttled;
        public readonly TimeSpan RetryDelay;
        public readonly int NextRetryDelayMilliseconds;

        public RetryResult(bool throttled, TimeSpan retryDelay, int nextRetryDelayMilliseconds)
        {
            this.Throttled = throttled;
            this.RetryDelay = retryDelay;
            this.NextRetryDelayMilliseconds = nextRetryDelayMilliseconds;
        }
    }
}
