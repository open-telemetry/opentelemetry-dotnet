// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class MetricReaderTests
{
    [Theory]
    [InlineData("counter", typeof(long), AggregationTemporality.Delta)]
    [InlineData("counter", typeof(double), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(long), AggregationTemporality.Delta)]
    [InlineData("histogram", typeof(double), AggregationTemporality.Delta)]
    [InlineData("updowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("updowncounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observablecounter", typeof(double), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(long), AggregationTemporality.Cumulative)]
    [InlineData("observableupdowncounter", typeof(double), AggregationTemporality.Cumulative)]
    public void LowMemoryTemporality_UsesCorrectAggregationTemporality(string instrumentName, Type valueType, AggregationTemporality expectedTemporality)
    {
        var metrics = new List<Metric>();
        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.LowMemory;
            })
            .Build();

        switch (instrumentName)
        {
            case "counter":
                if (valueType == typeof(long))
                {
                    meter.CreateCounter<long>("test_counter").Add(1);
                }
                else
                {
                    meter.CreateCounter<double>("test_counter").Add(1);
                }

                break;

            case "histogram":
                if (valueType == typeof(long))
                {
                    meter.CreateHistogram<long>("test_histogram").Record(1);
                }
                else
                {
                    meter.CreateHistogram<double>("test_histogram").Record(1);
                }

                break;

            case "updowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateUpDownCounter<long>("test_updown").Add(1);
                }
                else
                {
                    meter.CreateUpDownCounter<double>("test_updown").Add(1);
                }

                break;

            case "observablecounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableCounter("test_observable_counter", () => new Measurement<double>(1));
                }

                break;

            case "observableupdowncounter":
                if (valueType == typeof(long))
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<long>(1));
                }
                else
                {
                    meter.CreateObservableUpDownCounter("test_observable_updown", () => new Measurement<double>(1));
                }

                break;

            default:
                break;
        }

        provider.ForceFlush();

        var metric = Assert.Single(metrics);
        Assert.Equal(expectedTemporality, metric.Temporality);
    }

    [Fact]
    public async Task Collect_WhenConcurrentCallTimesOut_ReturnsFalseWithoutStartingAnotherCollection()
    {
        using var metricReader = new BlockingMetricReader();

        // Run each collection on a dedicated thread so the test does not depend on the
        // thread pool, which may be busy with other tests running at the same time.
        var firstCollectTask = RunOnDedicatedThread(() => metricReader.Collect(Timeout.Infinite));

        Assert.True(metricReader.CollectionStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        var secondCollectTask = RunOnDedicatedThread(() => metricReader.Collect(50));

        var firstCollectResult = false;

        try
        {
            Assert.False(await secondCollectTask);
            Assert.False(firstCollectTask.IsCompleted);
            Assert.Equal(1, metricReader.CollectCallCount);
        }
        finally
        {
            metricReader.AllowCollectionToFinish.Set();
            firstCollectResult = await firstCollectTask;
        }

        Assert.True(firstCollectResult);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(5_000)]
    public void Collect_PassesRemainingTimeoutToExporter(int timeoutMilliseconds)
    {
        int? exportTimeoutMilliseconds = null;

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new DelegatingExporter<Metric>();
#pragma warning restore CA2000 // Dispose objects before losing scope
        exporter.OnExportFunc = _ =>
        {
            exportTimeoutMilliseconds = exporter.ExportTimeoutMilliseconds;
            return ExportResult.Success;
        };

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddReader(new BaseExportingMetricReader(exporter))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build();

        meter.CreateCounter<long>("test_counter").Add(1);

        Assert.True(provider.ForceFlush(timeoutMilliseconds));

        Assert.NotNull(exportTimeoutMilliseconds);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            Assert.Equal(Timeout.Infinite, exportTimeoutMilliseconds);
        }
        else
        {
            Assert.InRange(exportTimeoutMilliseconds.Value, 1, timeoutMilliseconds);
        }
    }

    private static Task<bool> RunOnDedicatedThread(Func<bool> collect)
        => Task.Factory.StartNew(collect, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private sealed class BlockingMetricReader : MetricReader
    {
        public ManualResetEventSlim CollectionStarted { get; } = new();

        public ManualResetEventSlim AllowCollectionToFinish { get; } = new();

        public int CollectCallCount { get; private set; }

        protected override bool OnCollect(int timeoutMilliseconds)
        {
            this.CollectCallCount++;
            this.CollectionStarted.Set();

            return this.AllowCollectionToFinish.Wait(TimeSpan.FromSeconds(5));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.CollectionStarted.Dispose();
                this.AllowCollectionToFinish.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
