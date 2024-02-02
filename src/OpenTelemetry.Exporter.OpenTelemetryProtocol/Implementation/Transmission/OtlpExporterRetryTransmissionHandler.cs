// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Grpc.Core;
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
        var retryAttemptCount = 0;

        while (retryAttemptCount < 5 && this.ShouldRetryRequest(request, response, retryAttemptCount++, out var sleepDuration))
        {
            if (sleepDuration > TimeSpan.Zero)
            {
                Thread.Sleep(sleepDuration);
            }

            response = this.RetryRequest(request);
            if (response.Success)
            {
                return true;
            }
        }

        this.OnRequestDropped(request);

        return false;
    }

    protected virtual bool ShouldRetryRequest(TRequest request, ExportClientResponse response, int retryAttemptCount, out TimeSpan sleepDuration)
    {
        if (response is ExportClientGrpcResponse)
        {
            if (response.Exception is RpcException rpcException
            && OtlpRetry.TryGetGrpcRetryResult(rpcException.StatusCode, response.Deadline, rpcException.Trailers, retryAttemptCount, out var retryResult))
            {
                sleepDuration = retryResult.RetryDelay;
                return true;
            }
        }

        if (response is ExportClientHttpResponse httpResponse)
        {
            if (OtlpRetry.TryGetHttpRetryResult(httpResponse.StatusCode, response.Deadline, httpResponse.Headers, retryAttemptCount, out var retryResult))
            {
                sleepDuration = retryResult.RetryDelay;
                return true;
            }
        }

        sleepDuration = default;
        return false;
    }
}
