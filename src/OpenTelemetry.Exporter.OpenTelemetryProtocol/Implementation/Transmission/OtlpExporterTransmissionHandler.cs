// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal class OtlpExporterTransmissionHandler<TRequest>
{
    public OtlpExporterTransmissionHandler(IExportClient<TRequest> exportClient)
    {
        Guard.ThrowIfNull(exportClient);

        this.ExportClient = exportClient;
    }

    protected IExportClient<TRequest> ExportClient { get; }

    /// <summary>
    /// Sends export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
    public bool SubmitRequest(TRequest request)
    {
        try
        {
            var response = this.ExportClient.SendExportRequest(request);
            if (response.Success)
            {
                return true;
            }

            return this.OnSubmitRequestFailure(request, response);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            this.OnRequestDropped(request);
            return false;
        }
    }

    /// <summary>
    /// Attempts to shutdown the transmission handler, blocks the current thread
    /// until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <see langword="true" /> if shutdown succeeded; otherwise, <see
    /// langword="false" />.
    /// </returns>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.OnShutdown();

        return this.ExportClient.Shutdown(timeoutMilliseconds);
    }

    /// <summary>
    /// Fired when the transmission handler is shutdown.
    /// </summary>
    protected virtual void OnShutdown()
    {
    }

    /// <summary>
    /// Fired when a request could not be submitted.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <param name="response"><see cref="ExportClientResponse" />.</param>
    /// <returns><see langword="true" /> if the request will be resubmitted.</returns>
    protected virtual bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        this.OnRequestDropped(request);
        return false;
    }

    /// <summary>
    /// Fired when a request could not be submitted.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <returns><see cref="ExportClientResponse"/>.</returns>
    protected ExportClientResponse RetryRequest(TRequest request)
    {
        var response = this.ExportClient.SendExportRequest(request);
        if (!response.Success)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(response.Exception, isRetry: true);
        }

        return response;
    }

    /// <summary>
    /// Fired when a request is dropped.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    protected virtual void OnRequestDropped(TRequest request)
    {
    }
}
