// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal abstract class OtlpExporterRetryTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>
{
    protected OtlpExporterRetryTransmissionHandler(IExportClient<TRequest> exportClient)
        : base(exportClient)
    {
    }

    /// <summary>
    /// Retries sending request to the server.
    /// </summary>
    /// <param name="requestState">The request to send to the server.</param>
    /// <returns><see langword="true" /> if the request was sent successfully.</returns>
    protected bool RetryRequest(OtlpExporterRequestRetryState<TRequest> requestState)
    {
        try
        {
            this.OnRetryRequest(requestState.Request);

            var response = this.ExportClient.SendExportRequest(requestState.Request);
            if (response.Success)
            {
                return true;
            }

            return this.OnRetryRequestFailure(requestState, response);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex, isRetry: true);
            this.OnRequestDropped(requestState.Request);
            return false;
        }
    }

    /// <summary>
    /// Fired before a request is retried.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    protected virtual void OnRetryRequest(TRequest request)
    {
    }

    /// <inheritdoc/>
    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        var state = new OtlpExporterRequestRetryState<TRequest>(request, 1);

        if (this.ShouldRetryRequest(state, response))
        {
            this.StoreRequestForRetry(state);
            return true;
        }

        return false;
    }

    protected virtual bool OnRetryRequestFailure(OtlpExporterRequestRetryState<TRequest> state, ExportClientResponse response)
    {
        state.SubmissionCount++;

        if (this.ShouldRetryRequest(state, response))
        {
            this.StoreRequestForRetry(state);
            return true;
        }

        this.OnRequestDropped(state.Request);
        return false;
    }

    protected abstract void StoreRequestForRetry(OtlpExporterRequestRetryState<TRequest> state);

    private bool ShouldRetryRequest(OtlpExporterRequestRetryState<TRequest> state, ExportClientResponse response)
    {
        if (response is ExportClientHttpResponse httpResponse)
        {
            // TODO: Implement retry rules for http
            return false;
        }
        else if (response is ExportClientGrpcResponse grpcResponse)
        {
            // TODO: Implement retry rules for grpc
            return false;
        }
        else
        {
            Debug.Fail("Unknown ExportClientResponse type");
            return false;
        }
    }
}
