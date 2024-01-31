// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterPersistentStorageRetryTransmissionHandler<TRequest> : OtlpExporterRetryTransmissionHandler<TRequest>
{
    private readonly ManualResetEvent stopEvent = new(false);
    private readonly Thread thread;

    public OtlpExporterPersistentStorageRetryTransmissionHandler(IExportClient<TRequest> exportClient)
        : base(exportClient)
    {
        this.thread = new Thread(this.ProcessRetryRequestsThreadBody)
        {
            Name = $"OtlpExporter Persistent Retry Storage - {typeof(TRequest)}",
            IsBackground = true,
        };

        this.thread.Start();
    }

    protected override void StoreRequestForRetry(OtlpExporterRequestRetryState<TRequest> state)
    {
        // TODO: Add request to persistent storage
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
            // TODO: Implement logic for coming alive and sending records ready for retry

            // Should this fire on a delay or what?
            if (this.stopEvent.WaitOne(5000))
            {
                break;
            }

            // TODO: Read records for retry and call this.RetryRequest();
        }
    }
}
