// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;

/*
Measures the cost of handing a finished span to a BatchActivityExportProcessor
when several threads end spans at the same time. Times are per span end
(aggregate across threads); with perfect scaling the per-span time halves
each time the thread count doubles.
*/

namespace Benchmarks.Trace;

[MemoryDiagnoser(false)]
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class BatchActivityExportProcessorBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const int OperationsPerInvoke = 1 << 18;

    private ActivitySource? source;
    private ActivityListener? listener;
    private Activity? activity;
    private BatchActivityExportProcessor? processor;

    [Params(1, 4, 8)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.source = new ActivitySource("Benchmarks.BatchActivityExportProcessor");
        this.listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, this.source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.listener);

        this.activity = this.source.StartActivity("Benchmark")!;
        this.activity.Stop();

        // The queue is sized so that no span is dropped while the export worker
        // drains it in the background.
#pragma warning disable CA2000 // Dispose objects before losing scope - the processor owns and disposes the exporter
        this.processor = new BatchActivityExportProcessor(
            new NoopExporter(),
            maxQueueSize: 1 << 20,
            maxExportBatchSize: 512);
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.processor?.Dispose();
        this.listener?.Dispose();
        this.activity?.Dispose();
        this.source?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void OnEnd()
    {
        var threads = this.Threads;
        var operationsPerThread = OperationsPerInvoke / threads;

        if (threads == 1)
        {
            this.RunOnEnd(operationsPerThread);
            return;
        }

        var tasks = new Task[threads];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Factory.StartNew(
                () => this.RunOnEnd(operationsPerThread),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        Task.WaitAll(tasks);
    }

    private void RunOnEnd(int count)
    {
        var processor = this.processor!;
        var activity = this.activity!;

        for (var i = 0; i < count; i++)
        {
            processor.OnEnd(activity);
        }
    }

    private sealed class NoopExporter : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
            => ExportResult.Success;
    }
}
