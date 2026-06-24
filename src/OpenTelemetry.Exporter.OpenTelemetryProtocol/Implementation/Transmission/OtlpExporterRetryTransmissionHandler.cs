// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterRetryTransmissionHandler : OtlpExporterTransmissionHandler
{
    internal OtlpExporterRetryTransmissionHandler(IExportClient exportClient, double timeoutMilliseconds)
        : base(exportClient, timeoutMilliseconds)
    {
    }

    protected override bool OnSubmitRequestFailure(byte[] request, int contentLength, ExportClientResponse response)
    {
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;
        var noStatusRetryCount = 0;

        while (RetryHelper.ShouldRetryRequest(response, nextRetryDelayMilliseconds, out var retryResult))
        {
            // For no-status HTTP failures (e.g. HttpClient.Timeout) the original deadline is consumed
            // by the per-request timeout, so subsequent retries must use a fresh deadline. Cap these
            // retries to prevent indefinite looping when the server is unreachable.
            if (response is ExportClientHttpResponse { StatusCode: null } &&
                ++noStatusRetryCount > OtlpRetry.MaxNoStatusRetryAttempts)
            {
                break;
            }

            // Note: This delay cannot exceed the configured timeout period for otlp exporter.
            // If the backend responds with `RetryAfter` duration that would result in exceeding the configured timeout period
            // we would fail fast and drop the data.
            Thread.Sleep(retryResult.RetryDelay);

            // When the deadline in the response has already been consumed (e.g. by a
            // per-request HttpClient.Timeout), create a fresh deadline for this retry.
            var utcNow = DateTime.UtcNow;
            var retryDeadlineUtc = response.DeadlineUtc > utcNow
                ? response.DeadlineUtc
                : utcNow.AddMilliseconds(this.TimeoutMilliseconds);

            if (this.TryRetryRequest(request, contentLength, retryDeadlineUtc, out response))
            {
                return true;
            }

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        return false;
    }
}
