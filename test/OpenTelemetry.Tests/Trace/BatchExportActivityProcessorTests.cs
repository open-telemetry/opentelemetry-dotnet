// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public class BatchExportActivityProcessorTests
{
    [Fact]
    public void CheckNullExporter()
        => Assert.Throws<ArgumentNullException>(() => new BatchActivityExportProcessor(null!));

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
    [InlineData(10_000)]
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
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Empty(exportedItems);

        // forcing flush
        var result = processor.ForceFlush(timeout);

        Assert.Equal(timeout != 0, result);

        // Wait for the expected number of items to be exported
        var expectedCount = 2;

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

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(10_000)]
    public async Task CheckShutdownWaitsForInFlightOnEndCalls(int timeoutMilliseconds)
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(exporter);

        // Simulate an OnEnd call which has passed the shutdown
        // check but has not finished enqueueing yet.
        var token = processor.TryEnterOnEnd();
        Assert.True(token >= 0);

        var shutdown = Task.Run(() => processor.Shutdown(timeoutMilliseconds), TestContext.Current.CancellationToken);

        // Shutdown must not complete while the OnEnd call is still in flight
        var delay = Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        Assert.NotSame(shutdown, await Task.WhenAny(shutdown, delay));

        // Once shutdown has started new OnEnd calls are rejected...
        Assert.True(processor.TryEnterOnEnd() < 0);

        // ...and shutdown completes once the in-flight call exits.
        processor.ExitOnEnd(token);

        Assert.True(await WaitForShutdownAsync(shutdown));
    }

    [Fact]
    public async Task CheckShutdownStopsWaitingForInFlightOnEndCallsWhenTimeoutExpires()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new BatchActivityExportProcessor(exporter);

        var token = processor.TryEnterOnEnd();
        Assert.True(token >= 0);

        var timeoutMilliseconds = 500;
        var startedAt = Stopwatch.GetTimestamp();

        var shutdown = Task.Run(() => processor.Shutdown(timeoutMilliseconds), TestContext.Current.CancellationToken);

        // The in-flight call never exits so shutdown must give up waiting
        // for it once the caller's budget is exhausted rather than hang.
        await WaitForShutdownAsync(shutdown);

        Assert.True(Stopwatch.GetElapsedTime(startedAt) >= TimeSpan.FromMilliseconds(timeoutMilliseconds));

        processor.ExitOnEnd(token);
    }

    [Fact]
    public async Task CheckOnEndRacingShutdownIsAlwaysRejectedOrWaitedFor()
    {
        // TryEnterOnEnd checks the shutdown flag both before and after it
        // registers the in-flight call. The second check only fails when
        // Shutdown runs in between, which cannot be forced, so the two are
        // raced repeatedly instead. If an in-flight call which lost that race
        // were not unregistered again Shutdown would wait for it forever.
        var threads = Math.Max(2, Math.Min(Environment.ProcessorCount, 4));

        for (var iteration = 0; iteration < 100; iteration++)
        {
            using var exporter = new InMemoryExporter<Activity>(new List<Activity>());
            using var processor = new BatchActivityExportProcessor(exporter);
            using var start = new ManualResetEventSlim();

            var workers = new Task[threads];

            for (var i = 0; i < threads; i++)
            {
                workers[i] = Task.Run(
                    () =>
                    {
                        start.Wait(TestContext.Current.CancellationToken);

                        int token;
                        while ((token = processor.TryEnterOnEnd()) >= 0)
                        {
                            processor.ExitOnEnd(token);
                        }

                        // Once rejected an OnEnd call must stay rejected
                        Assert.True(processor.TryEnterOnEnd() < 0);
                    },
                    TestContext.Current.CancellationToken);
            }

            start.Set();

            var shutdown = Task.Run(() => processor.Shutdown(), TestContext.Current.CancellationToken);

            Assert.True(await WaitForShutdownAsync(shutdown));

            await Task.WhenAll(workers);
        }
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

    [Fact]
    public void CheckExporterTimeoutIsPassedToExporter()
    {
        DateTime? exportStartedAtUtc = null;
        DateTime? exportDeadlineUtc = null;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Activity>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            exportDeadlineUtc = exporter.ExportDeadlineUtc;
            exportStartedAtUtc = DateTime.UtcNow;
            return ExportResult.Success;
        };

        using var processor = new BatchActivityExportProcessor(
            exporter,
            exporterTimeoutMilliseconds: 1234);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        var before = DateTime.UtcNow;
        processor.OnEnd(activity);
        processor.Shutdown();

        Assert.NotNull(exportDeadlineUtc);
        Assert.NotNull(exportStartedAtUtc);
        Assert.InRange(exportDeadlineUtc.Value, before.AddMilliseconds(1234), exportStartedAtUtc.Value.AddMilliseconds(1234));

        // The deadline only applies to the export it was passed to
        Assert.Equal(DateTime.MaxValue, exporter.ExportDeadlineUtc);
    }

    private static async Task<bool> WaitForShutdownAsync(Task<bool> shutdown)
    {
        var timeout = Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Same(shutdown, await Task.WhenAny(shutdown, timeout));

        return await shutdown;
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
