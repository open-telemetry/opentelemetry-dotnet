// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class TaskWorkerTests
{
    private const int BaselineCollectCount = 2;
    private const int CircularBufferCapacity = 8;
    private const int IdleCyclesBeforeTrigger = 2;
    private const int MaxExportBatchSize = 4;
    private const int PostBaselinePauseMilliseconds = 20;
    private const int TriggerCompletionTimeoutMilliseconds = 100;
    private const int WaitUntilPollingIntervalMilliseconds = 10;
    private const int WorkerDelayMilliseconds = 250;
    private const int WorkerTimeoutMilliseconds = 1000;

    [Fact]
    public async Task BatchExportTaskWorker_TriggerExportAfterIdleCycles_DoesNotWaitForScheduledDelay()
    {
        // Arrange
        var circularBuffer = new CircularBuffer<Activity>(capacity: CircularBufferCapacity);
        using var exporter = new TestActivityExporter();
        using var worker = new BatchExportTaskWorker<Activity>(
            circularBuffer,
            exporter,
            maxExportBatchSize: MaxExportBatchSize,
            scheduledDelayMilliseconds: WorkerDelayMilliseconds,
            exporterTimeoutMilliseconds: WorkerTimeoutMilliseconds);

        worker.Start();

        await Task.Delay(GetIdleWaitDuration());

        using var activity = new Activity("test");

        Assert.True(circularBuffer.Add(activity));
        Assert.True(worker.TriggerExport());

        // Act
        await WaitUntilAsync(() => exporter.ExportCount >= 1, TriggerCompletionTimeoutMilliseconds);

        // Assert
        Assert.True(worker.Shutdown(WorkerTimeoutMilliseconds));
    }

    [Fact]
    public async Task PeriodicExportingMetricReaderTaskWorker_TriggerExportAfterIdleCycles_DoesNotWaitForExportInterval()
    {
        // Arrange
        using var reader = new TestMetricReader();
        using var worker = new PeriodicExportingMetricReaderTaskWorker(
            reader,
            exportIntervalMilliseconds: WorkerDelayMilliseconds,
            exportTimeoutMilliseconds: WorkerTimeoutMilliseconds);

        worker.Start();

        await WaitUntilAsync(() => reader.CollectCount >= BaselineCollectCount, WorkerTimeoutMilliseconds);

        var baselineCollectCount = reader.CollectCount;

        await Task.Delay(PostBaselinePauseMilliseconds);

        Assert.True(worker.TriggerExport());

        // Act
        await WaitUntilAsync(() => reader.CollectCount >= baselineCollectCount + 1, TriggerCompletionTimeoutMilliseconds);

        // Assert
        Assert.True(worker.Shutdown(WorkerTimeoutMilliseconds));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(WaitUntilPollingIntervalMilliseconds);
        }

        Assert.True(condition());
    }

    private static int GetIdleWaitDuration()
        => (IdleCyclesBeforeTrigger * WorkerDelayMilliseconds) + (WorkerDelayMilliseconds / 5);

    private sealed class TestActivityExporter : BaseExporter<Activity>
    {
        private int exportCount;

        public int ExportCount => this.exportCount;

        public override ExportResult Export(in Batch<Activity> batch)
        {
            Interlocked.Increment(ref this.exportCount);
            return ExportResult.Success;
        }
    }

#pragma warning disable CA2000 // BaseExportingMetricReader owns the exporter lifecycle
    private sealed class TestMetricReader() : BaseExportingMetricReader(new TestMetricExporter())
#pragma warning restore CA2000
    {
        private int collectCount;

        public int CollectCount => this.collectCount;

        protected override bool OnCollect(int timeoutMilliseconds)
        {
            Interlocked.Increment(ref this.collectCount);
            return true;
        }
    }

    private sealed class TestMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
    }
}
