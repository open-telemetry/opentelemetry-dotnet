// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal static class RetryHelper
{
    internal static bool ShouldRetryRequest<TRequest>(TRequest request, ExportClientResponse response, int retryDelayMilliseconds, out OtlpRetry.RetryResult retryResult)
    {
        if (response is ExportClientGrpcResponse grpcResponse)
        {
            if (OtlpRetry.TryGetGrpcRetryResult(grpcResponse, retryDelayMilliseconds, out retryResult))
            {
                return true;
            }
        }
        else if (response is ExportClientHttpResponse httpResponse)
        {
            if (OtlpRetry.TryGetHttpRetryResult(httpResponse, retryDelayMilliseconds, out retryResult))
            {
                return true;
            }
        }

        retryResult = default;
        return false;
    }
}
