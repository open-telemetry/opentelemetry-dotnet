// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal static class RetryHelper
{
    internal static bool ShouldRetryRequest<TRequest>(TRequest request, ExportClientResponse response, int retryDelayMilliseconds, out OtlpRetry.RetryResult retryResult)
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
