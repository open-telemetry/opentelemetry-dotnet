// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.ExporterOpenTelemetryProtocol.Implementation.Retry;

internal class OtlpExporterTransmissionHandler<T>
{
    internal IExportClient<T>? ExportClient;

    public OtlpExporterOptions? Options { get; internal set; }

    /// <summary>
    /// Sends export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
    public virtual bool SubmitRequest(T request)
    {
        try
        {
            return this.ExportClient == null ? false : this.ExportClient.SendExportRequest(request);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return this.OnSubmitRequestExceptionThrown(request, ex);
        }
    }

    /// <summary>
    /// Retries sending request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="exception">Exception encountered when trying to send request.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
    protected virtual bool RetryRequest(T request, out Exception? exception)
    {
        try
        {
            var result = this.ExportClient == null ? false : this.ExportClient.SendExportRequest(request);
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
    /// <param name="exception">Exception that was encountered during request processing.</param>
    /// <returns>True or False, based on the implementation of handling errors.</returns>
    protected virtual bool OnSubmitRequestExceptionThrown(T request, Exception exception)
    {
        return this.OnHandleDroppedRequest(request);
    }

    /// <summary>
    /// Action to take when dropping request.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <returns>True or False, based on the implementation.</returns>
    protected virtual bool OnHandleDroppedRequest(T request)
    {
        return false;
    }
}
