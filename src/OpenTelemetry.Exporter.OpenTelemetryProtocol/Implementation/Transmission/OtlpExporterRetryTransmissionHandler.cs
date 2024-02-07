// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal class OtlpExporterRetryTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>
{
    internal OtlpExporterRetryTransmissionHandler(IExportClient<TRequest> exportClient)
        : base(exportClient)
    {
    }

    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;
        while (RetryHelper.ShouldRetryRequest(request, response, nextRetryDelayMilliseconds, out var retryResult))
        {
            Thread.Sleep(retryResult.RetryDelay);

            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;

            response = this.RetryRequest(request);
            if (response.Success)
            {
                return true;
            }
        }

        this.OnRequestDropped(request);

        return false;
    }
}
