// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class BatchExportActivityProcessorTests
{
    [Fact]
    public void CheckNullExporter()
    {
        Assert.Throws<ArgumentNullException>(() => new BatchActivityExportProcessor(null!));
    }

    [Fact]
    public void CheckConstructorWithInvalidValues()
    {
        var exportedItems = new List<Activity>();
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxQueueSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxExportBatchSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxQueueSize: 1, maxExportBatchSize: 2049));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), scheduledDelayMilliseconds: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), exporterTimeoutMilliseconds: -1));
    }

    [Fact]
    public async Task CheckIfBatchIsExportingOnQueueLimit()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 1,
            maxExportBatchSize: 1,
            scheduledDelayMilliseconds: 100_000);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity);

        await WaitForMinimumCountAsync(exportedItems, 1);

        Assert.Single(exportedItems);

        Assert.Equal(1, processor.ProcessedCount);
        Assert.Equal(1, processor.ReceivedCount);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Fact]
    public void CheckForceFlushWithInvalidTimeout()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(exporter, maxQueueSize: 2, maxExportBatchSize: 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.ForceFlush(-2));
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CheckForceFlushExport(int timeout)
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 3,
            maxExportBatchSize: 3,
            exporterTimeoutMilliseconds: 30000);

        using var activity1 = new Activity("start1")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        using var activity2 = new Activity("start2")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity1);
        processor.OnEnd(activity2);

        Assert.Equal(0, processor.ProcessedCount);

        // waiting to see if time is triggering the exporter
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Empty(exportedItems);

        // forcing flush
        var result = processor.ForceFlush(timeout);

        Assert.Equal(timeout != 0, result);

        // Wait for the expected number of items to be exported
        int expectedCount = 2;

        await WaitForMinimumCountAsync(exportedItems, expectedCount);

        Assert.Equal(expectedCount, exportedItems.Count);

        Assert.Equal(expectedCount, processor.ProcessedCount);
        Assert.Equal(expectedCount, processor.ReceivedCount);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CheckShutdownExport(int timeoutMilliseconds)
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 3,
            maxExportBatchSize: 3,
            exporterTimeoutMilliseconds: 30000);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity);
        processor.Shutdown(timeoutMilliseconds);

        await WaitForMinimumCountAsync(exportedItems, 1);

        Assert.Single(exportedItems);

        Assert.Equal(1, processor.ProcessedCount);
        Assert.Equal(1, processor.ReceivedCount);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Fact]
    public void CheckExportForRecordingButNotSampledActivity()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(
            exporter,
            maxQueueSize: 1,
            maxExportBatchSize: 1);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.None,
        };

        processor.OnEnd(activity);
        processor.Shutdown();

        Assert.Empty(exportedItems);
        Assert.Equal(0, processor.ProcessedCount);
    }

    [Fact]
    public void CheckExportDrainsBatchOnFailure()
    {
        using var processor = new BatchActivityExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            exporter: new FailureExporter<Activity>(),
#pragma warning restore CA2000 // Dispose objects before losing scope
            maxQueueSize: 3,
            maxExportBatchSize: 3);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity);
        processor.OnEnd(activity);
        processor.OnEnd(activity);
        processor.Shutdown();

        Assert.Equal(3, processor.ProcessedCount); // Verify batch was drained even though nothing was exported.
    }

    private static async Task WaitForMinimumCountAsync(List<Activity> collection, int minimum)
    {
        var maximumWait = TimeSpan.FromSeconds(5);
        var waitInterval = TimeSpan.FromSeconds(0.25);

        using var cts = new CancellationTokenSource(maximumWait);

        // We check for a minimum because if there are too many it's better to
        // terminate the loop and let the assert in the caller fail immediately
        while (!cts.IsCancellationRequested && collection.Count < minimum)
        {
            await Task.Delay(waitInterval);
        }
    }

    private sealed class FailureExporter<T> : BaseExporter<T>
        where T : class
    {
        public override ExportResult Export(in Batch<T> batch) => ExportResult.Failure;
    }
}
