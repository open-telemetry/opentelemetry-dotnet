// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterPersistentStorageTransmissionHandler : OtlpExporterTransmissionHandler, IDisposable
{
    private const int RetryIntervalInMilliseconds = 60000;
    private readonly ManualResetEvent shutdownEvent = new(false);
    private readonly ManualResetEvent dataExportNotification = new(false);
    private readonly AutoResetEvent exportEvent = new(false);
    private readonly Thread thread;
    private readonly PersistentBlobProvider persistentBlobProvider;
    private bool disposed;

    public OtlpExporterPersistentStorageTransmissionHandler(IExportClient exportClient, double timeoutMilliseconds, string storagePath)
#pragma warning disable CA2000 // Dispose objects before losing scope
        : this(new FileBlobProvider(storagePath), exportClient, timeoutMilliseconds)
#pragma warning restore CA2000 // Dispose objects before losing scope
    {
    }

    internal OtlpExporterPersistentStorageTransmissionHandler(PersistentBlobProvider persistentBlobProvider, IExportClient exportClient, double timeoutMilliseconds)
        : base(exportClient, timeoutMilliseconds)
    {
        Debug.Assert(persistentBlobProvider != null, "persistentBlobProvider was null");
        this.persistentBlobProvider = persistentBlobProvider!;

        this.thread = new Thread(this.RetryStoredRequests)
        {
            Name = "OtlpExporter Persistent Retry Storage",
            IsBackground = true,
        };

        this.thread.Start();
    }

    // Used for test.
    internal bool InitiateAndWaitForRetryProcess(int timeOutMilliseconds)
    {
        this.exportEvent.Set();

        return this.dataExportNotification.WaitOne(timeOutMilliseconds);
    }

    protected override bool OnSubmitRequestFailure(byte[] request, int contentLength, ExportClientResponse response)
    {
        Debug.Assert(request != null, "request was null");
        return RetryHelper.ShouldRetryRequest(response, OtlpRetry.InitialBackoffMilliseconds, out _) && this.persistentBlobProvider.TryCreateBlob(request!, out _);
    }

    protected override void OnShutdown(int timeoutMilliseconds)
    {
        var sw = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.StartNew();

        try
        {
            this.shutdownEvent.Set();
        }
        catch (ObjectDisposedException)
        {
            // Dispose was called before shutdown.
        }

        this.thread.Join(timeoutMilliseconds);

        if (sw != null)
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

            base.OnShutdown((int)Math.Max(timeout, 0));
        }
        else
        {
            base.OnShutdown(timeoutMilliseconds);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.shutdownEvent.Dispose();
                this.exportEvent.Dispose();
                this.dataExportNotification.Dispose();
                (this.persistentBlobProvider as IDisposable)?.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private void RetryStoredRequests()
    {
        var handles = new WaitHandle[] { this.shutdownEvent, this.exportEvent };
        while (true)
        {
            try
            {
                var index = WaitHandle.WaitAny(handles, RetryIntervalInMilliseconds);
                if (index == 0)
                {
                    // Shutdown signaled
                    break;
                }

                int fileCount = 0;

                // TODO: Run maintenance job.
                // Transmit 10 files at a time.
                while (fileCount < 10 && !this.shutdownEvent.WaitOne(0))
                {
                    if (!this.persistentBlobProvider.TryGetBlob(out var blob))
                    {
                        break;
                    }

                    if (blob.TryLease((int)this.TimeoutMilliseconds) && blob.TryRead(out var data))
                    {
                        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);
                        if (this.TryRetryRequest(data, data.Length, deadlineUtc, out var response) || !RetryHelper.ShouldRetryRequest(response, OtlpRetry.InitialBackoffMilliseconds, out var retryInfo))
                        {
                            blob.TryDelete();
                        }

                        // TODO: extend the lease period based on the response from server on retryAfter.
                    }

                    fileCount++;
                }

                // Set and reset the handle to notify export and wait for next signal.
                // This is used for InitiateAndWaitForRetryProcess.
                this.dataExportNotification.Set();
                this.dataExportNotification.Reset();
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.RetryStoredRequestException(ex);
                return;
            }
        }
    }
}
