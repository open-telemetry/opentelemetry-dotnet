// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public sealed class MetricPointTests : IDisposable
{
    private readonly Meter meter;
    private readonly MeterProvider meterProvider;
    private readonly Metric metric;
    private readonly Histogram<long> histogram;
    private readonly double[] bounds;
    private MetricPoint metricPoint;

    public MetricPointTests()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogram = this.meter.CreateHistogram<long>("histogram");

        // Evenly distribute the bound values over the range [0, MaxValue)
        this.bounds = new double[10];
        for (var i = 0; i < this.bounds.Length; i++)
        {
            this.bounds[i] = i * 1000 / this.bounds.Length;
        }

        var exportedItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(exportedItems)
            .AddView(this.histogram.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
            .Build();

        this.histogram.Record(500);

        this.meterProvider.ForceFlush();

        this.metric = exportedItems[0];
        var metricPointsEnumerator = this.metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        this.metricPoint = metricPointsEnumerator.Current;
    }

    public void Dispose()
    {
        this.meter?.Dispose();
        this.meterProvider.Dispose();
    }

    [Fact]
    public void VerifyMetricPointCopy()
    {
        var copy = this.metricPoint.Copy();

        // Verify these structs are unique instances.
        Assert.NotEqual(copy, this.metricPoint);

        // Verify properties are copied.
        Assert.Equal(copy.Tags, this.metricPoint.Tags);
        Assert.Equal(copy.StartTime, this.metricPoint.StartTime);
        Assert.Equal(copy.EndTime, this.metricPoint.EndTime);
    }

    [Fact]
    public void VerifyHistogramBucketsCopy()
    {
        var histogramBuckets = this.metricPoint.GetHistogramBuckets();
        var copy = histogramBuckets.Copy();

        // Verify these are unique instances.
        Assert.False(ReferenceEquals(copy, histogramBuckets));
        Assert.NotSame(copy, histogramBuckets);

        // Verify fields are copied
        Assert.NotSame(copy.RunningBucketCounts, histogramBuckets.RunningBucketCounts);
        Assert.Equal(copy.RunningBucketCounts, histogramBuckets.RunningBucketCounts);
        Assert.NotSame(copy.SnapshotBucketCounts, histogramBuckets.SnapshotBucketCounts);
        Assert.Equal(copy.SnapshotBucketCounts, histogramBuckets.SnapshotBucketCounts);
        Assert.Equal(copy.SnapshotSum, histogramBuckets.SnapshotSum);
    }

    [Fact]
    public void PublishedMetricPointIsNotInitializedBeforeItIsFullyWritten()
    {
        // A collect treats a MetricPoint as valid as soon as IsInitialized is true, so it must not
        // become true before the rest of the MetricPoint (such as its Tags) has been written.
        const int Count = 100_000;

        var bounds = new HistogramExplicitBounds(this.bounds);
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var metricPoints = new MetricPoint[Count];

        var current = -1;
        var torn = 0;

        var reader = new Thread(() =>
        {
            int index;

            while ((index = Volatile.Read(ref current)) < Count)
            {
                if (index >= 0)
                {
                    ref var metricPoint = ref metricPoints[index];

                    if (metricPoint.IsInitialized && metricPoint.Tags.KeyAndValues is null)
                    {
                        torn++;
                    }
                }
            }
        });

        reader.Start();

        for (var i = 0; i < Count; i++)
        {
            Volatile.Write(ref current, i);

            MetricPoint.Publish(
                ref metricPoints[i],
                this.metric.AggregatorStore,
                AggregationType.HistogramWithBuckets,
                tags,
                bounds,
                Metric.DefaultExponentialHistogramMaxBuckets,
                Metric.DefaultExponentialHistogramMaxScale);
        }

        Volatile.Write(ref current, Count);
        reader.Join();

        Assert.Equal(0, torn);
    }
}
