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
        int attempt = 0;
        while (RetryHelper.ShouldRetryRequest(response, nextRetryDelayMilliseconds, attempt, out var retryResult))
        {
            // Note: This delay cannot exceed the configured timeout period for otlp exporter.
            // If the backend responds with `RetryAfter` duration that would result in exceeding the configured timeout period
            // we would fail fast and drop the data.
            Thread.Sleep(retryResult.RetryDelay);

            if (this.TryRetryRequest(request, contentLength, response.DeadlineUtc, out response))
            {
                return true;
            }

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
            attempt++;
        }

        return false;
    }
}
