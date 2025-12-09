// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricSnapshotTests
{
    [Fact]
    public void VerifySnapshot_Counter()
    {
        var exportedMetrics = new List<Metric>();
        var exportedSnapshots = new List<MetricSnapshot>();

        using var meter = new Meter(new MeterOptions(Utils.GetCurrentMethodName())
        {
            Version = "1.0.0",
            TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.0.0",
        });

        var counter = meter.CreateCounter<long>("meter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedMetrics)
            .AddInMemoryExporter(exportedSnapshots)
            .Build();

        // FIRST EXPORT
        counter.Add(10);
        meterProvider.ForceFlush();

        // Verify Metric 1
        Assert.Single(exportedMetrics);
        var metric1 = exportedMetrics[0];
        var metricPoints1Enumerator = metric1.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints1Enumerator.MoveNext());
        ref readonly var metricPoint1 = ref metricPoints1Enumerator.Current;
        Assert.Equal(10, metricPoint1.GetSumLong());

        // Verify Snapshot 1
        Assert.Single(exportedSnapshots);
        var snapshot1 = exportedSnapshots[0];
        Assert.Single(snapshot1.MetricPoints);
        Assert.Equal(10, snapshot1.MetricPoints[0].GetSumLong());

        // Verify Metric == Snapshot
        Assert.Equal(metric1.Name, snapshot1.Name);
        Assert.Equal(metric1.Description, snapshot1.Description);
        Assert.Equal(metric1.Unit, snapshot1.Unit);
        Assert.Equal(metric1.MeterName, snapshot1.MeterName);
        Assert.Equal(metric1.MetricType, snapshot1.MetricType);
        Assert.Equal(metric1.MeterVersion, snapshot1.MeterVersion);
        Assert.Equal(metric1.MeterSchemaUrl, snapshot1.MeterSchemaUrl);

        // SECOND EXPORT
        counter.Add(5);
        meterProvider.ForceFlush();

        // Verify Metric 1, after second export
        // This value is expected to be updated.
        Assert.Equal(15, metricPoint1.GetSumLong());

        // Verify Metric 2
        Assert.Equal(2, exportedMetrics.Count);
        var metric2 = exportedMetrics[1];
        var metricPoints2Enumerator = metric2.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints2Enumerator.MoveNext());
        ref readonly var metricPoint2 = ref metricPoints2Enumerator.Current;
        Assert.Equal(15, metricPoint2.GetSumLong());

        // Verify Snapshot 1, after second export
        // This value is expected to be unchanged.
        Assert.Equal(10, snapshot1.MetricPoints[0].GetSumLong());

        // Verify Snapshot 2
        Assert.Equal(2, exportedSnapshots.Count);
        var snapshot2 = exportedSnapshots[1];

        Assert.Single(snapshot2.MetricPoints);

        Assert.Equal(15, snapshot2.MetricPoints[0].GetSumLong());
    }

    [Fact]
    public void VerifySnapshot_Histogram()
    {
        var exportedMetrics = new List<Metric>();
        var exportedSnapshots = new List<MetricSnapshot>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogram = meter.CreateHistogram<int>("histogram");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedMetrics)
            .AddInMemoryExporter(exportedSnapshots)
            .Build();

        // FIRST EXPORT
        histogram.Record(10);
        meterProvider.ForceFlush();

        // Verify Metric 1
        Assert.Single(exportedMetrics);
        var metric1 = exportedMetrics[0];
        var metricPoints1Enumerator = metric1.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints1Enumerator.MoveNext());
        ref readonly var metricPoint1 = ref metricPoints1Enumerator.Current;
        Assert.Equal(1, metricPoint1.GetHistogramCount());
        Assert.Equal(10, metricPoint1.GetHistogramSum());
        metricPoint1.TryGetHistogramMinMaxValues(out var min, out var max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);

        // Verify Snapshot 1
        Assert.Single(exportedSnapshots);
        var snapshot1 = exportedSnapshots[0];
        Assert.Single(snapshot1.MetricPoints);
        Assert.Equal(1, snapshot1.MetricPoints[0].GetHistogramCount());
        Assert.Equal(10, snapshot1.MetricPoints[0].GetHistogramSum());
        snapshot1.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);

        // Verify Metric == Snapshot
        Assert.Equal(metric1.Name, snapshot1.Name);
        Assert.Equal(metric1.Description, snapshot1.Description);
        Assert.Equal(metric1.Unit, snapshot1.Unit);
        Assert.Equal(metric1.MeterName, snapshot1.MeterName);
        Assert.Equal(metric1.MetricType, snapshot1.MetricType);
        Assert.Equal(metric1.MeterVersion, snapshot1.MeterVersion);

        // SECOND EXPORT
        histogram.Record(5);
        meterProvider.ForceFlush();

        // Verify Metric 1 after second export
        // This value is expected to be updated.
        Assert.Equal(2, metricPoint1.GetHistogramCount());
        Assert.Equal(15, metricPoint1.GetHistogramSum());
        metricPoint1.TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);

        // Verify Metric 2
        Assert.Equal(2, exportedMetrics.Count);
        var metric2 = exportedMetrics[1];
        var metricPoints2Enumerator = metric2.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints2Enumerator.MoveNext());
        ref readonly var metricPoint2 = ref metricPoints2Enumerator.Current;
        Assert.Equal(2, metricPoint2.GetHistogramCount());
        Assert.Equal(15, metricPoint2.GetHistogramSum());
        metricPoint2.TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);

        // Verify Snapshot 1 after second export
        // This value is expected to be unchanged.
        Assert.Equal(1, snapshot1.MetricPoints[0].GetHistogramCount());
        Assert.Equal(10, snapshot1.MetricPoints[0].GetHistogramSum());
        snapshot1.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);

        // Verify Snapshot 2
        Assert.Equal(2, exportedSnapshots.Count);
        var snapshot2 = exportedSnapshots[1];
        Assert.Single(snapshot2.MetricPoints);
        Assert.Equal(2, snapshot2.MetricPoints[0].GetHistogramCount());
        Assert.Equal(15, snapshot2.MetricPoints[0].GetHistogramSum());
        snapshot2.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);
    }

    [Fact]
    public void VerifySnapshot_ExponentialHistogram()
    {
        var expectedHistogram = new Base2ExponentialBucketHistogram();
        var exportedMetrics = new List<Metric>();
        var exportedSnapshots = new List<MetricSnapshot>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogram = meter.CreateHistogram<int>("histogram");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("histogram", new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(exportedMetrics)
            .AddInMemoryExporter(exportedSnapshots)
            .Build();

        // FIRST EXPORT
        expectedHistogram.Record(10);
        histogram.Record(10);
        meterProvider.ForceFlush();

        // Verify Metric 1
        Assert.Single(exportedMetrics);
        var metric1 = exportedMetrics[0];
        var metricPoints1Enumerator = metric1.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints1Enumerator.MoveNext());
        ref readonly var metricPoint1 = ref metricPoints1Enumerator.Current;
        Assert.Equal(1, metricPoint1.GetHistogramCount());
        Assert.Equal(10, metricPoint1.GetHistogramSum());
        metricPoint1.TryGetHistogramMinMaxValues(out var min, out var max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);
        AggregatorTests.AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint1.GetExponentialHistogramData());

        // Verify Snapshot 1
        Assert.Single(exportedSnapshots);
        var snapshot1 = exportedSnapshots[0];
        Assert.Single(snapshot1.MetricPoints);
        Assert.Equal(1, snapshot1.MetricPoints[0].GetHistogramCount());
        Assert.Equal(10, snapshot1.MetricPoints[0].GetHistogramSum());
        snapshot1.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);
        AggregatorTests.AssertExponentialBucketsAreCorrect(expectedHistogram, snapshot1.MetricPoints[0].GetExponentialHistogramData());

        // Verify Metric == Snapshot
        Assert.Equal(metric1.Name, snapshot1.Name);
        Assert.Equal(metric1.Description, snapshot1.Description);
        Assert.Equal(metric1.Unit, snapshot1.Unit);
        Assert.Equal(metric1.MeterName, snapshot1.MeterName);
        Assert.Equal(metric1.MetricType, snapshot1.MetricType);
        Assert.Equal(metric1.MeterVersion, snapshot1.MeterVersion);

        // SECOND EXPORT
        expectedHistogram.Record(5);
        histogram.Record(5);
        meterProvider.ForceFlush();

        // Verify Metric 1 after second export
        // This value is expected to be updated.
        Assert.Equal(2, metricPoint1.GetHistogramCount());
        Assert.Equal(15, metricPoint1.GetHistogramSum());
        metricPoint1.TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);

        // Verify Metric 2
        Assert.Equal(2, exportedMetrics.Count);
        var metric2 = exportedMetrics[1];
        var metricPoints2Enumerator = metric2.GetMetricPoints().GetEnumerator();
        Assert.True(metricPoints2Enumerator.MoveNext());
        ref readonly var metricPoint2 = ref metricPoints2Enumerator.Current;
        Assert.Equal(2, metricPoint2.GetHistogramCount());
        Assert.Equal(15, metricPoint2.GetHistogramSum());
        metricPoint1.TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);
        AggregatorTests.AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint2.GetExponentialHistogramData());

        // Verify Snapshot 1 after second export
        // This value is expected to be unchanged.
        Assert.Equal(1, snapshot1.MetricPoints[0].GetHistogramCount());
        Assert.Equal(10, snapshot1.MetricPoints[0].GetHistogramSum());
        snapshot1.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(10, min);
        Assert.Equal(10, max);

        // Verify Snapshot 2
        Assert.Equal(2, exportedSnapshots.Count);
        var snapshot2 = exportedSnapshots[1];
        Assert.Single(snapshot2.MetricPoints);
        Assert.Equal(2, snapshot2.MetricPoints[0].GetHistogramCount());
        Assert.Equal(15, snapshot2.MetricPoints[0].GetHistogramSum());
        snapshot2.MetricPoints[0].TryGetHistogramMinMaxValues(out min, out max);
        Assert.Equal(5, min);
        Assert.Equal(10, max);
        AggregatorTests.AssertExponentialBucketsAreCorrect(expectedHistogram, snapshot2.MetricPoints[0].GetExponentialHistogramData());
    }
}
