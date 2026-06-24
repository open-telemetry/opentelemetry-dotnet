// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
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

        using var transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(persistentBlobProvider, exportClient, timeoutMilliseconds: 10_000);

        byte[] request = [1, 2, 3, 4, 9, 9, 9];
        var result = transmissionHandler.TrySubmitRequest(request, contentLength: 4);

        Assert.True(result);
        Assert.NotNull(persistentBlobProvider.LastBuffer);
        Assert.Equal([1, 2, 3, 4], persistentBlobProvider.LastBuffer);
    }

    [Fact]
    public void TrySubmitRequest_PersistsWhenDeadlineAlreadyExceeded()
    {
        // Regression test for https://github.com/open-telemetry/opentelemetry-dotnet/issues/7444.
        // A retryable failure must still be persisted to disk even when the export deadline
        // has already been exceeded by the time the request fails. Otherwise the data is
        // dropped instead of being saved for a later retry.
        var exportClient = new FailingExportClient(deadlineExceeded: true);
        var persistentBlobProvider = new CapturingBlobProvider();

        using var transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(persistentBlobProvider, exportClient, timeoutMilliseconds: 10_000);

        byte[] request = [1, 2, 3, 4, 9, 9, 9];
        var result = transmissionHandler.TrySubmitRequest(request, contentLength: 4);

        Assert.True(result);
        Assert.NotNull(persistentBlobProvider.LastBuffer);
        Assert.Equal([1, 2, 3, 4], persistentBlobProvider.LastBuffer);
    }

    [Fact]
    public void TrySubmitRequest_DoesNotPersistNonRetryableFailure()
    {
        var exportClient = new FailingExportClient(statusCode: HttpStatusCode.BadRequest);
        var persistentBlobProvider = new CapturingBlobProvider();

        using var transmissionHandler = new OtlpExporterPersistentStorageTransmissionHandler(persistentBlobProvider, exportClient, timeoutMilliseconds: 10_000);

        byte[] request = [1, 2, 3, 4, 9, 9, 9];
        var result = transmissionHandler.TrySubmitRequest(request, contentLength: 4);

        Assert.False(result);
        Assert.Null(persistentBlobProvider.LastBuffer);
    }

    private sealed class FailingExportClient : IExportClient
    {
        private readonly HttpStatusCode statusCode;
        private readonly bool deadlineExceeded;

        public FailingExportClient(HttpStatusCode statusCode = HttpStatusCode.ServiceUnavailable, bool deadlineExceeded = false)
        {
            this.statusCode = statusCode;
            this.deadlineExceeded = deadlineExceeded;
        }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default) =>
            new ExportClientHttpResponse(
                success: false,
                deadlineUtc: this.deadlineExceeded ? DateTime.UtcNow.AddMilliseconds(-1) : deadlineUtc,
#pragma warning disable CA2000 //  Dispose objects before losing scope
                response: new HttpResponseMessage(this.statusCode),
#pragma warning restore CA2000 //  Dispose objects before losing scope
                exception: null);

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

        protected override bool OnTryGetBlob(out PersistentBlob blob)
        {
            blob = new NoopBlob();
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
