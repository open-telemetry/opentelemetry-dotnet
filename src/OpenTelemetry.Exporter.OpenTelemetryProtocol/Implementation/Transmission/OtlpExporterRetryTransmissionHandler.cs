// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterRetryTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>
{
    internal OtlpExporterRetryTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds)
        : base(exportClient, timeoutMilliseconds)
    {
    }

    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;
        while (RetryHelper.ShouldRetryRequest(request, response, nextRetryDelayMilliseconds, out var retryResult))
        {
            // Note: This delay cannot exceed the configured timeout period for otlp exporter.
            // If the backend responds with `RetryAfter` duration that would result in exceeding the configured timeout period
            // we would fail fast and drop the data.
            Thread.Sleep(retryResult.RetryDelay);

            if (this.TryRetryRequest(request, response.DeadlineUtc, out response))
            {
                return true;
            }

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        return false;
    }
}
