// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterPersistentStorageTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>, IDisposable
{
    private const int RetryIntervalInMilliseconds = 60000;
    private readonly ManualResetEvent shutdownEvent = new(false);
    private readonly ManualResetEvent dataExportNotification = new(false);
    private readonly AutoResetEvent exportEvent = new(false);
    private readonly Thread thread;
    private readonly PersistentBlobProvider persistentBlobProvider;
    private readonly Func<byte[], TRequest> requestFactory;
    private bool disposed;

    public OtlpExporterPersistentStorageTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory, string storagePath)
        : this(new FileBlobProvider(storagePath), exportClient, timeoutMilliseconds, requestFactory)
    {
    }

    internal OtlpExporterPersistentStorageTransmissionHandler(PersistentBlobProvider persistentBlobProvider, IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory)
        : base(exportClient, timeoutMilliseconds)
    {
        Debug.Assert(persistentBlobProvider != null, "persistentBlobProvider was null");
        Debug.Assert(requestFactory != null, "requestFactory was null");

        this.persistentBlobProvider = persistentBlobProvider!;
        this.requestFactory = requestFactory!;

        this.thread = new Thread(this.RetryStoredRequests)
        {
            Name = $"OtlpExporter Persistent Retry Storage - {typeof(TRequest)}",
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

    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        if (RetryHelper.ShouldRetryRequest(request, response, OtlpRetry.InitialBackoffMilliseconds, out _))
        {
            byte[]? data = null;
            if (request is ExportTraceServiceRequest traceRequest)
            {
                data = traceRequest.ToByteArray();
            }
            else if (request is ExportMetricsServiceRequest metricsRequest)
            {
                data = metricsRequest.ToByteArray();
            }
            else if (request is ExportLogsServiceRequest logsRequest)
            {
                data = logsRequest.ToByteArray();
            }
            else
            {
                Debug.Fail("Unexpected request type encountered");
                data = null;
            }

            if (data != null)
            {
                return this.persistentBlobProvider.TryCreateBlob(data, out _);
            }
        }

        return false;
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
                        var request = this.requestFactory.Invoke(data);
                        if (this.TryRetryRequest(request, deadlineUtc, out var response) || !RetryHelper.ShouldRetryRequest(request, response, OtlpRetry.InitialBackoffMilliseconds, out var retryInfo))
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
