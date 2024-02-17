// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;
using Xunit;

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
        for (int i = 0; i < this.bounds.Length; i++)
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
        Assert.NotSame(copy.BucketCounts, histogramBuckets.BucketCounts);
        Assert.Equal(copy.BucketCounts, histogramBuckets.BucketCounts);
        Assert.Equal(copy.SnapshotSum, histogramBuckets.SnapshotSum);
    }
}
