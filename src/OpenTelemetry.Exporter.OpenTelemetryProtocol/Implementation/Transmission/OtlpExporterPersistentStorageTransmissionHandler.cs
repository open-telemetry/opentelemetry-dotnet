// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

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
    internal int RetryInterval = 60000;
    private readonly ManualResetEvent shutdownEvent = new(false);
    private readonly ManualResetEvent dataExportNotification = new(false);
    private readonly AutoResetEvent exportEvent = new(false);
    private readonly Thread thread;
    private readonly PersistentBlobProvider persistentBlobProvider;
    private readonly Func<byte[], TRequest>? requestFactory;
    private bool disposed;

    public OtlpExporterPersistentStorageTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory, string storagePath)
        : this(new FileBlobProvider(storagePath), exportClient, timeoutMilliseconds, requestFactory)
    {
    }

    internal OtlpExporterPersistentStorageTransmissionHandler(PersistentBlobProvider persistentBlobProvider, IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory)
        : base(exportClient, timeoutMilliseconds)
    {
        this.persistentBlobProvider = persistentBlobProvider;
        this.requestFactory = requestFactory;

        this.thread = new Thread(this.RetryStoredRequests)
        {
            Name = $"OtlpExporter Persistent Retry Storage - {typeof(TRequest)}",
            IsBackground = true,
        };

        this.thread.Start();
    }

    // Used for test.
    internal bool Forceflush(int timeOutMilliseconds)
    {
        this.exportEvent.Set();

        if (this.dataExportNotification.WaitOne(timeOutMilliseconds))
        {
            return true;
        }

        return false;
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

            if (data != null)
            {
                this.persistentBlobProvider?.TryCreateBlob(data, out _);
            }

            return true;
        }

        return false;
    }

    protected override void OnShutdown(int timeoutMilliseconds)
    {
        this.shutdownEvent.Set();
        this.thread.Join(timeoutMilliseconds);
        base.OnShutdown(timeoutMilliseconds);
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
                (this.persistentBlobProvider as FileBlobProvider)?.Dispose();
            }

            this.disposed = true;
        }
    }

    private void RetryStoredRequests()
    {
        while (true)
        {
            try
            {
                if (this.shutdownEvent.WaitOne(0))
                {
                    break;
                }

                // Wait 60 seconds before retrying
                this.exportEvent.WaitOne(this.RetryInterval);

                int fileCount = 0;

                // Transmit 10 files at a time.
                while (fileCount < 10 && !this.shutdownEvent.WaitOne(0))
                {
                    if (this.persistentBlobProvider.TryGetBlob(out var blob))
                    {
                        if (blob.TryLease((int)this.TimeoutMilliseconds) && blob.TryRead(out var data))
                        {
                            if (this.requestFactory != null)
                            {
                                var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);
                                var request = this.requestFactory.Invoke(data);
                                if (this.TryRetryRequest(request, deadlineUtc, out var response) || !RetryHelper.ShouldRetryRequest(request, response, OtlpRetry.InitialBackoffMilliseconds, out _))
                                {
                                    blob.TryDelete();
                                }
                            }
                        }
                    }
                    else
                    {
                        break;
                    }

                    fileCount++;
                }

                this.dataExportNotification.Set();
                this.dataExportNotification.Reset();
            }
            catch (ObjectDisposedException)
            {
                // the handler is somehow disposed before the worker thread could finish its job
                return;
            }
        }
    }
}
