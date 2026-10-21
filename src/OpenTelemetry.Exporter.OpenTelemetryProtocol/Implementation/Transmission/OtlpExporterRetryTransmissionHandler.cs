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
        while (RetryHelper.ShouldRetryRequest(response, nextRetryDelayMilliseconds, out var retryResult))
        {
            // Note: This delay cannot exceed the configured timeout period for otlp exporter.
            // If the backend responds with `RetryAfter` duration that would result in exceeding the configured timeout period
            // we would fail fast and drop the data.
            Thread.Sleep(retryResult.RetryDelay);

            var remainingTime = response.DeadlineUtc - DateTime.UtcNow;
            if (remainingTime <= TimeSpan.Zero)
            {
                OpenTelemetryProtocolExporterEventSource.Log.RetryDeadlineExceeded();
                return false;
            }

            // HttpClient.Timeout applies separately to each request. Bound this
            // attempt by the time left in the original export budget instead.
            using var deadlineCancellation = new CancellationTokenSource(remainingTime);
            try
            {
                if (this.TryRetryRequest(request, contentLength, response.DeadlineUtc, out response, deadlineCancellation.Token))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (deadlineCancellation.IsCancellationRequested)
            {
                OpenTelemetryProtocolExporterEventSource.Log.RetryDeadlineExceeded();
                return false;
            }

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        return false;
    }
}
