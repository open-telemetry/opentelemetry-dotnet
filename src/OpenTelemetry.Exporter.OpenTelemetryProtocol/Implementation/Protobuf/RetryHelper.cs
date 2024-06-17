// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal static class RetryHelper
{
    internal static bool ShouldRetryRequest(ExportClientResponse response, int retryDelayMilliseconds, out OtlpRetry.RetryResult retryResult)
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
