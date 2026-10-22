// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal class OtlpExporterTransmissionHandler : IDisposable
{
    public OtlpExporterTransmissionHandler(IExportClient exportClient, double timeoutMilliseconds)
    {
        Guard.ThrowIfNull(exportClient);

        this.ExportClient = exportClient;
        this.TimeoutMilliseconds = timeoutMilliseconds;
    }

    internal IExportClient ExportClient { get; }

    internal double TimeoutMilliseconds { get; }

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
        => this.TrySubmitRequest(request, contentLength, this.GetDeadlineUtc(Timeout.Infinite));

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
    /// <param name="timeoutMilliseconds">
    /// The number of milliseconds the caller allows the export to take, or zero
    /// or <see cref="Timeout.Infinite"/> when the caller does not set a limit.
    /// The shorter of this and the exporter's own timeout is used for the deadline.
    /// </param>
    /// <returns>
    /// The deadline time in utc for the export to finish, or <see cref="DateTime.MaxValue"/>
    /// if neither the caller nor the exporter limits how long the export can take.
    /// </returns>
    public DateTime GetDeadlineUtc(int timeoutMilliseconds)
    {
        var effectiveTimeoutMilliseconds = this.TimeoutMilliseconds;

        // The exporter's own timeout is not positive when it has no limit (for
        // example an HttpClient with an infinite Timeout), so the caller's applies.
        if (timeoutMilliseconds > 0 &&
            (effectiveTimeoutMilliseconds <= 0 || timeoutMilliseconds < effectiveTimeoutMilliseconds))
        {
            effectiveTimeoutMilliseconds = timeoutMilliseconds;
        }

        return effectiveTimeoutMilliseconds > 0
            ? DateTime.UtcNow.AddMilliseconds(effectiveTimeoutMilliseconds)
            : DateTime.MaxValue;
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

        this.OnShutdown(timeoutMilliseconds);

        if (timestamp is { } startedAt)
        {
            timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt);
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
