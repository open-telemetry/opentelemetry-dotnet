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

    [Fact]
    public void RetryStoredRequests_ThreadSurvivesExportClientException()
    {
        // Pre-fix: the exception kills the thread and the second InitiateAndWaitForRetryProcess
        // call times out. Use a real timeout so a dead thread does not hang the test.
        // The RetainingBlobProvider re-exposes the same blob after SimulateLeaseExpiry, proving
        // that the blob itself is eventually transmitted rather than silently abandoned.
        var exportClient = new ThrowOnceThenSucceedExportClient();
        var blobProvider = new RetainingBlobProvider();
        var blob1 = new TrackingBlob([1, 2, 3]);
        blobProvider.SetBlob(blob1);

        using var handler = new OtlpExporterPersistentStorageTransmissionHandler(blobProvider, exportClient, timeoutMilliseconds: 10_000);

        Assert.True(handler.InitiateAndWaitForRetryProcess(5_000), "thread must survive an export client exception");
        Assert.False(blob1.WasDeleted, "blob must be retained when the export client throws");

        // Simulate lease expiry so the same blob is re-exposed on the next pass.
        blobProvider.SimulateLeaseExpiry();

        Assert.True(handler.InitiateAndWaitForRetryProcess(5_000), "thread must still be alive on the second pass");
        Assert.True(blob1.WasDeleted, "blob must be deleted after successful transmission on the second pass");
    }

    [Fact]
    public void RetryStoredRequests_ContinuesProcessingRemainingBlobsAfterException()
    {
        // When one blob causes the export client to throw, the remaining blobs in the same
        // pass must still be processed rather than the loop aborting.
        var exportClient = new ThrowOnceThenSucceedExportClient();
        var blobProvider = new QueueBlobProvider();

        using var handler = new OtlpExporterPersistentStorageTransmissionHandler(blobProvider, exportClient, timeoutMilliseconds: 10_000);

        var blob1 = new TrackingBlob([1, 2, 3]);
        var blob2 = new TrackingBlob([4, 5, 6]);
        blobProvider.Enqueue(blob1);
        blobProvider.Enqueue(blob2);

        Assert.True(handler.InitiateAndWaitForRetryProcess(5_000));
        Assert.False(blob1.WasDeleted, "blob1 must be retained when the export client throws");
        Assert.True(blob2.WasDeleted, "blob2 must be processed in the same pass after blob1 throws");
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

    private sealed class ThrowOnceThenSucceedExportClient : IExportClient
    {
        private int callCount;

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.callCount) == 1)
            {
                throw new InvalidOperationException("Simulated export client failure.");
            }

            return new ExportClientHttpResponse(success: true, deadlineUtc: deadlineUtc, response: null, exception: null);
        }

        public bool Shutdown(int timeoutMilliseconds) => true;
    }

    private sealed class QueueBlobProvider : PersistentBlobProvider
    {
        private readonly Queue<PersistentBlob> queue = new();

        public void Enqueue(PersistentBlob blob) => this.queue.Enqueue(blob);

        protected override IEnumerable<PersistentBlob> OnGetBlobs() => this.queue;

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob)
        {
            blob = default!;
            return false;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob)
        {
            blob = default!;
            return false;
        }

        protected override bool OnTryGetBlob(out PersistentBlob blob)
        {
            if (this.queue.Count > 0)
            {
                blob = this.queue.Dequeue();
                return true;
            }

            blob = default!;
            return false;
        }
    }

    // Models a persistent blob whose lease expires and is re-discovered on a later pass.
    // SetBlob makes the blob available once; SimulateLeaseExpiry makes it available again,
    // matching what FileBlobProvider does when a lease period elapses.
    private sealed class RetainingBlobProvider : PersistentBlobProvider
    {
        private TrackingBlob? blob;
        private bool available;

        public void SetBlob(TrackingBlob blob)
        {
            this.blob = blob;
            this.available = true;
        }

        public void SimulateLeaseExpiry() => this.available = true;

        protected override IEnumerable<PersistentBlob> OnGetBlobs() => [];

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob)
        {
            blob = default!;
            return false;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob)
        {
            blob = default!;
            return false;
        }

        protected override bool OnTryGetBlob(out PersistentBlob blob)
        {
            if (this.available && this.blob != null && !this.blob.WasDeleted)
            {
                blob = this.blob;
                this.available = false;
                return true;
            }

            blob = default!;
            return false;
        }
    }

    private sealed class TrackingBlob : PersistentBlob
    {
        private readonly byte[] data;

        public TrackingBlob(byte[] data)
        {
            this.data = data;
        }

        public bool WasDeleted { get; private set; }

        protected override bool OnTryRead(out byte[] buffer)
        {
            buffer = this.data;
            return true;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0) => true;

        protected override bool OnTryLease(int leasePeriodMilliseconds) => true;

        protected override bool OnTryDelete()
        {
            this.WasDeleted = true;
            return true;
        }
    }
}
