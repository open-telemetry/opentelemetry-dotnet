// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal class OtlpExporterTransmissionHandler<TRequest> : IDisposable
{
    public OtlpExporterTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds)
    {
        Guard.ThrowIfNull(exportClient);

        this.ExportClient = exportClient;
        this.TimeoutMilliseconds = timeoutMilliseconds;
    }

    internal IExportClient<TRequest> ExportClient { get; }

    internal double TimeoutMilliseconds { get; }

    /// <summary>
    /// Attempts to send an export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <returns> <see langword="true" /> if the request is sent successfully; otherwise, <see
    /// langword="false" />.
    /// </returns>
    public bool TrySubmitRequest(TRequest request)
    {
        try
        {
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);
            var response = this.ExportClient.SendExportRequest(request, deadlineUtc);
            if (response.Success)
            {
                return true;
            }

            return this.OnSubmitRequestFailure(request, response);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.TrySubmitRequestException(ex);
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
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        var sw = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.StartNew();

        this.OnShutdown(timeoutMilliseconds);

        if (sw != null)
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

            return this.ExportClient.Shutdown((int)Math.Max(timeout, 0));
        }

        return this.ExportClient.Shutdown(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Fired when the transmission handler is shutdown.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    protected virtual void OnShutdown(int timeoutMilliseconds)
    {
    }

    /// <summary>
    /// Fired when a request could not be submitted.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <param name="response"><see cref="ExportClientResponse" />.</param>
    /// <returns><see langword="true" /> If the request is resubmitted and succeeds; otherwise, <see
    /// langword="false" />.</returns>
    protected virtual bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        return false;
    }

    /// <summary>
    /// Fired when resending a request to the server.
    /// </summary>
    /// <param name="request">The request to be resent to the server.</param>
    /// <param name="deadlineUtc">The deadline time in utc for export request to finish.</param>
    /// <param name="response"><see cref="ExportClientResponse" />.</param>
    /// <returns><see langword="true" /> If the retry succeeds; otherwise, <see
    /// langword="false" />.</returns>
    protected bool TryRetryRequest(TRequest request, DateTime deadlineUtc, out ExportClientResponse response)
    {
        response = this.ExportClient.SendExportRequest(request, deadlineUtc);
        if (!response.Success)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(response.Exception, isRetry: true);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally
    /// releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
