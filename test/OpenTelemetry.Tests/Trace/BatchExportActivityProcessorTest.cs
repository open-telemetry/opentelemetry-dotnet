// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class BatchExportActivityProcessorTest
{
    [Fact]
    public void CheckNullExporter()
    {
        Assert.Throws<ArgumentNullException>(() => new BatchActivityExportProcessor(null));
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
    public void CheckIfBatchIsExportingOnQueueLimit()
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

        for (int i = 0; i < 10 && exportedItems.Count == 0; i++)
        {
            Thread.Sleep(500);
        }

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
    public void CheckForceFlushExport(int timeout)
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
        Thread.Sleep(1_000);
        Assert.Empty(exportedItems);

        // forcing flush
        processor.ForceFlush(timeout);

        if (timeout == 0)
        {
            // ForceFlush(0) will trigger flush and return immediately, so let's sleep for a while
            Thread.Sleep(1_000);
        }

        Assert.Equal(2, exportedItems.Count);

        Assert.Equal(2, processor.ProcessedCount);
        Assert.Equal(2, processor.ReceivedCount);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public void CheckShutdownExport(int timeoutMilliseconds)
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

        if (timeoutMilliseconds < 1_000)
        {
            Thread.Sleep(1_000 - timeoutMilliseconds);
        }

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
            exporter: new FailureExporter<Activity>(),
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

    private class FailureExporter<T> : BaseExporter<T>
        where T : class
    {
        public override ExportResult Export(in Batch<T> batch) => ExportResult.Failure;
    }
}
