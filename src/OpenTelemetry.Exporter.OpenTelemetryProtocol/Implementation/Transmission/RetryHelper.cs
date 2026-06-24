// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

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

    /// <summary>
    /// Determines whether a failed export response represents a transient
    /// failure that is eligible to be retried, ignoring any deadline.
    /// </summary>
    /// <param name="response">The <see cref="ExportClientResponse"/> to check.</param>
    /// <returns>
    /// <see langword="true"/> if the failure is retryable;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool ShouldRetryRequest(ExportClientResponse response) => response switch
    {
        ExportClientGrpcResponse grpcResponse => OtlpRetry.IsRetryable(grpcResponse),
        ExportClientHttpResponse httpResponse => OtlpRetry.IsRetryable(httpResponse),
        _ => false,
    };
}
