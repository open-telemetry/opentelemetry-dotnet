// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Internal.Tests;

public class PeriodicExportingMetricReaderWorkerTests
{
    private const int WaitTimeoutMilliseconds = 10_000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InfiniteIntervalSupportsTriggeredAndShutdownCollections(bool useTaskWorker)
    {
        using var collections = new SemaphoreSlim(0);
        using var reader = new TestMetricReader(collections);
        using var worker = CreateWorker(reader, Timeout.Infinite, useTaskWorker);

        worker.Start();

        try
        {
            Assert.True(worker.TriggerExport());
            Assert.True(collections.Wait(WaitTimeoutMilliseconds, TestContext.Current.CancellationToken));
            Assert.True(worker.Shutdown(WaitTimeoutMilliseconds));
            Assert.Equal(2, reader.CollectCount);
        }
        finally
        {
            worker.Shutdown(WaitTimeoutMilliseconds);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeriodicCollectionsContinueUntilShutdown(bool useTaskWorker)
    {
        using var collections = new SemaphoreSlim(0);
        using var reader = new TestMetricReader(collections);
        using var worker = CreateWorker(reader, 100, useTaskWorker);

        worker.Start();

        try
        {
            Assert.True(collections.Wait(WaitTimeoutMilliseconds, TestContext.Current.CancellationToken));
            Assert.True(collections.Wait(WaitTimeoutMilliseconds, TestContext.Current.CancellationToken));
            Assert.True(worker.Shutdown(WaitTimeoutMilliseconds));
            Assert.True(reader.CollectCount >= 3);
        }
        finally
        {
            worker.Shutdown(WaitTimeoutMilliseconds);
        }
    }

    private static PeriodicExportingMetricReaderWorker CreateWorker(TestMetricReader reader, int intervalMilliseconds, bool useTaskWorker)
    {
        if (useTaskWorker)
        {
            return new PeriodicExportingMetricReaderTaskWorker(reader, intervalMilliseconds, Timeout.Infinite);
        }

#if NET
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException();
        }
#endif

        return new PeriodicExportingMetricReaderThreadWorker(reader, intervalMilliseconds, Timeout.Infinite);
    }

#pragma warning disable CA2000 // BaseExportingMetricReader owns the exporter lifecycle.
    private sealed class TestMetricReader(SemaphoreSlim collections) : BaseExportingMetricReader(new TestMetricExporter())
#pragma warning restore CA2000
    {
        private int collectCount;

        public int CollectCount => Volatile.Read(ref this.collectCount);

        protected override bool OnCollect(int timeoutMilliseconds)
        {
            Interlocked.Increment(ref this.collectCount);
            collections.Release();
            return true;
        }
    }

    private sealed class TestMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
    }
}
