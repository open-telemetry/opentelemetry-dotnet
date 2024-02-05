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

internal sealed class OtlpExporterPersistentStorageRetryTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>
{
    private readonly ManualResetEvent stopEvent = new(false);
    private readonly Thread thread;
    private readonly PersistentBlobProvider persistentBlobProvider;
    private readonly Func<byte[], TRequest>? requestFactory;

    public OtlpExporterPersistentStorageRetryTransmissionHandler(IExportClient<TRequest> exportClient, Func<byte[], TRequest> requestFactory, string storagePath)
        : base(exportClient)
    {
        this.persistentBlobProvider = new FileBlobProvider(storagePath);
        this.requestFactory = requestFactory;

        this.thread = new Thread(this.ProcessRetryRequestsThreadBody)
        {
            Name = $"OtlpExporter Persistent Retry Storage - {typeof(TRequest)}",
            IsBackground = true,
        };

        this.thread.Start();
    }

    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
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

    protected override void OnShutdown()
    {
        this.stopEvent.Set();
        this.thread.Join();
        base.OnShutdown();
    }

    private void ProcessRetryRequestsThreadBody()
    {
        while (true)
        {
            // Wait before retrying
            if (this.stopEvent.WaitOne(5000))
            {
                break;
            }

            int fileCount = 0;

            // transmit 10 files at a time.
            while (fileCount < 10)
            {
                if (this.persistentBlobProvider != null && this.persistentBlobProvider.TryGetBlob(out var blob))
                {
                    if (blob != null && blob.TryLease(20000) && blob.TryRead(out var data))
                    {
                        if (this.requestFactory != null)
                        {
                            var request = this.requestFactory.Invoke(data);
                            var response = this.RetryRequest(request);
                            if (response.Success)
                            {
                                blob.TryDelete();
                            }

                            // TODO: return request resources
                        }
                    }
                }
                else
                {
                    break;
                }

                fileCount++;
            }
        }
    }
}
