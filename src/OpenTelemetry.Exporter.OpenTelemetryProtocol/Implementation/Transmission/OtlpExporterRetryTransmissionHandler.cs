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
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;
        while (this.ShouldRetryRequest(request, response, nextRetryDelayMilliseconds, out var retryResult))
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

    protected virtual bool ShouldRetryRequest(TRequest request, ExportClientResponse response, int retryDelayMilliseconds, out OtlpRetry.RetryResult retryResult)
    {
        if (response is ExportClientGrpcResponse)
        {
            if (response.Exception is RpcException rpcException
            && OtlpRetry.TryGetGrpcRetryResult(rpcException.StatusCode, response.DeadlineUtc, rpcException.Trailers, retryDelayMilliseconds, out retryResult))
            {
                return true;
            }
        }

        if (response is ExportClientHttpResponse httpResponse)
        {
            if (OtlpRetry.TryGetHttpRetryResult(httpResponse.StatusCode, response.DeadlineUtc, httpResponse.Headers, retryDelayMilliseconds, out retryResult))
            {
                return true;
            }
        }

        retryResult = default;
        return false;
    }
}
