// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.PersistentStorage.Abstractions;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission.Tests;

public class OtlpExporterPersistentStorageTransmissionHandlerTests
{
    [Fact]
    public void TrySubmitRequest_FailurePersistsOnlyContentLength()
    {
        var exportClient = new FailingExportClient();
        var persistentBlobProvider = new CapturingBlobProvider();

        using var transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(persistentBlobProvider, exportClient, timeoutMilliseconds: 1000);

        var request = new byte[] { 1, 2, 3, 4, 9, 9, 9 };
        var result = transmissionHandler.TrySubmitRequest(request, contentLength: 4);

        Assert.False(result);
        Assert.NotNull(persistentBlobProvider.LastBuffer);
        Assert.Equal([1, 2, 3, 4], persistentBlobProvider.LastBuffer);
    }

    private sealed class FailingExportClient : IExportClient
    {
        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            return new ExportClientHttpResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                response: new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                exception: null);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

    private sealed class CapturingBlobProvider : PersistentBlobProvider
    {
        public byte[]? LastBuffer { get; private set; }

        protected override IEnumerable<PersistentBlob> OnGetBlobs() => [];

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob)
        {
            this.LastBuffer = buffer;
            blob = new NoopBlob();
            return true;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob)
        {
            this.LastBuffer = buffer;
            blob = new NoopBlob();
            return true;
        }

        protected override bool OnTryGetBlob(out PersistentBlob? blob)
        {
            blob = null;
            return false;
        }
    }

    private sealed class NoopBlob : PersistentBlob
    {
        protected override bool OnTryRead(out byte[] buffer)
        {
            buffer = [];
            return false;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0) => true;

        protected override bool OnTryLease(int leasePeriodMilliseconds) => false;

        protected override bool OnTryDelete() => true;
    }
}
