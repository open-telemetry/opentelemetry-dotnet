// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Retry;

internal class OtlpExporterTransmissionHandler<TRequest, TResponse>
    where TResponse : class
{
    internal IExportClient<TRequest, TResponse>? ExportClient;

    public OtlpExporterOptions? Options { get; internal set; }

    /// <summary>
    /// Sends export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
    public virtual bool SubmitRequest(TRequest request)
    {
        TResponse? response = null;
        try
        {
            return this.ExportClient != null && this.ExportClient.SendExportRequest(request, out response);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return this.OnSubmitRequestExceptionThrown(request, response, ex);
        }
    }

    /// <summary>
    /// Retries sending request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="response">Response received on retry.</param>
    /// <param name="exception">Exception encountered when trying to send request.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
    protected virtual bool RetryRequest(TRequest request, out TResponse? response, out Exception? exception)
    {
        response = null;
        try
        {
            var result = this.ExportClient != null && this.ExportClient.SendExportRequest(request, out response);
            exception = null;
            return result;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex, isRetry: true);
            exception = ex;
            return false;
        }
    }

    /// <summary>
    /// Callback to call when encountered exception while sending request to server.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <param name="response">response object.</param>
    /// <param name="exception">Exception that was encountered during request processing.</param>
    /// <returns>True or False, based on the implementation of handling errors.</returns>
    protected virtual bool OnSubmitRequestExceptionThrown(TRequest request, TResponse? response, Exception exception)
    {
        return this.OnHandleDroppedRequest(request, response);
    }

    /// <summary>
    /// Action to take when dropping request.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <param name="response">The response recieved from server.</param>
    /// <returns>True or False, based on the implementation.</returns>
    protected virtual bool OnHandleDroppedRequest(TRequest request, TResponse? response)
    {
        return false;
    }
}
