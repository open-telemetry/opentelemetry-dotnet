// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal class OtlpExporterTransmissionHandler : IDisposable
{
    private readonly double initialTimeoutMilliseconds;
    private int shutdown;
    private int disposed;

    public OtlpExporterTransmissionHandler(IExportClient exportClient, double timeoutMilliseconds)
    {
        Guard.ThrowIfNull(exportClient);

        this.ExportClient = exportClient;
        this.initialTimeoutMilliseconds = timeoutMilliseconds;
    }

    internal IExportClient ExportClient { get; }

    internal double TimeoutMilliseconds => this.ExportClient is ReloadableExportClient reloadable
        ? reloadable.TimeoutMilliseconds
        : this.initialTimeoutMilliseconds;

    /// <summary>
    /// Attempts to send an export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="contentLength">length of content.</param>
    /// <returns>
    /// <see langword="true"/> if the request is sent successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TrySubmitRequest(byte[] request, int contentLength)
        => this.TrySubmitRequest(request, contentLength, this.GetDeadlineUtc(DateTime.MaxValue));

    /// <summary>
    /// Attempts to send an export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="contentLength">length of content.</param>
    /// <param name="deadlineUtc">
    /// The deadline time in utc for the export to finish, as returned by <see cref="GetDeadlineUtc"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the request is sent successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TrySubmitRequest(byte[] request, int contentLength, DateTime deadlineUtc)
    {
        try
        {
            var response = this.ExportClient.SendExportRequest(request, contentLength, deadlineUtc);

            return response.Success || this.OnSubmitRequestFailure(request, contentLength, response);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.TrySubmitRequestException(ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the deadline for an export which starts now.
    /// </summary>
    /// <param name="deadlineUtc">
    /// The time in utc by which the caller requires the export to finish, or
    /// <see cref="DateTime.MaxValue"/> when the caller does not set a limit.
    /// The earlier of this and the deadline from the exporter's own timeout is used.
    /// </param>
    /// <returns>
    /// The deadline time in utc for the export to finish, or <see cref="DateTime.MaxValue"/>
    /// if neither the caller nor the exporter limits how long the export can take.
    /// </returns>
    public DateTime GetDeadlineUtc(DateTime deadlineUtc)
    {
        // The exporter's own timeout is not positive when it has no limit (for
        // example an HttpClient with an infinite Timeout), so only the caller's applies.
        if (this.TimeoutMilliseconds > 0)
        {
            var exporterDeadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

            if (exporterDeadlineUtc < deadlineUtc)
            {
                deadlineUtc = exporterDeadlineUtc;
            }
        }

        return deadlineUtc;
    }

    /// <summary>
    /// Attempts to shutdown the transmission handler, blocks the current thread
    /// until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <see cref="Timeout.Infinite"/> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <see langword="true"/> if shutdown succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Shutdown(int timeoutMilliseconds)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        try
        {
            return Interlocked.Exchange(ref this.shutdown, 1) == 0
                && this.ExportClient.Shutdown(timeoutMilliseconds);
        }
        finally
        {
            if (timestamp is { } startedAt)
            {
                timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt);
            }

            // Cancel pending exports before waiting for a retry worker to finish.
            this.OnShutdown(timeoutMilliseconds);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        using (this.ExportClient as IDisposable)
        {
            try
            {
                this.Shutdown(Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // A caller-owned HTTP client may have been disposed already.
            }
            finally
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
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
    /// <param name="contentLength">Length of content.</param>
    /// <param name="response"><see cref="ExportClientResponse" />.</param>
    /// <returns>
    /// <see langword="true"/> If the request is resubmitted and succeeds; otherwise, <see langword="false"/>.
    /// </returns>
    protected virtual bool OnSubmitRequestFailure(byte[] request, int contentLength, ExportClientResponse response) => false;

    /// <summary>
    /// Fired when resending a request to the server.
    /// </summary>
    /// <param name="request">The request to be resent to the server.</param>
    /// <param name="contentLength">Length of content.</param>
    /// <param name="deadlineUtc">The deadline time in utc for export request to finish.</param>
    /// <param name="response"><see cref="ExportClientResponse" />.</param>
    /// <returns>
    /// <see langword="true"/> If the retry succeeds; otherwise, <see langword="false"/>.
    /// </returns>
    protected bool TryRetryRequest(byte[] request, int contentLength, DateTime deadlineUtc, out ExportClientResponse response)
    {
        response = this.ExportClient.SendExportRequest(request, contentLength, deadlineUtc);
        return response.Success;
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
