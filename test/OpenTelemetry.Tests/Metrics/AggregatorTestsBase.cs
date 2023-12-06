// <copyright file="AggregatorTestsBase.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics.Metrics;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable SA1402

public abstract class AggregatorTestsBase
{
    private static readonly Meter Meter = new("testMeter");
    private static readonly Instrument Instrument = Meter.CreateHistogram<long>("testInstrument");
    private static readonly ExplicitBucketHistogramConfiguration HistogramConfiguration = new() { Boundaries = Metric.DefaultHistogramBounds };
    private static readonly MetricStreamIdentity MetricStreamIdentity = new(Instrument, HistogramConfiguration);

    private readonly bool emitOverflowAttribute;
    private readonly bool shouldReclaimUnusedMetricPoints;
    private readonly AggregatorStore aggregatorStore;

    protected AggregatorTestsBase(bool emitOverflowAttribute, bool shouldReclaimUnusedMetricPoints)
    {
        this.emitOverflowAttribute = emitOverflowAttribute;
        this.shouldReclaimUnusedMetricPoints = shouldReclaimUnusedMetricPoints;

        this.aggregatorStore = new(
            MetricStreamIdentity,
            MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double,
            AggregationTemporality.Cumulative,
            1024,
            emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints);
    }

    [Fact]
    public void HistogramDistributeToAllBucketsDefault()
    {
        var histogramPoint = new MetricPoint(this.aggregatorStore, null, Metric.DefaultHistogramBounds, Metric.DefaultExponentialHistogramMaxBuckets, Metric.DefaultExponentialHistogramMaxScale);

        var measurementHandler = this.aggregatorStore.MeasurementHandler;

        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, -1D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 0D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 2D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 5D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 8D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 10D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 11D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 25D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 40D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 50D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 70D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 75D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 99D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 100D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 246D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 250D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 499D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 500D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 501D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 750D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 751D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 1000D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 1001D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 2500D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 2501D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 5000D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 5001D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 7500D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 7501D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 10000D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 10001D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 10000000D, tags: default);

        measurementHandler.CollectMeasurementsOnMetricPoint(ref histogramPoint);

        var count = histogramPoint.GetHistogramCount();

        Assert.Equal(32, count);

        int actualCount = 0;
        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            Assert.Equal(2, histogramMeasurement.BucketCount);
            actualCount++;
        }
    }

    [Fact]
    public void HistogramDistributeToAllBucketsCustom()
    {
        var boundaries = new double[] { 10, 20 };

        var histogramPoint = new MetricPoint(this.aggregatorStore, null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets, Metric.DefaultExponentialHistogramMaxScale);

        var measurementHandler = this.aggregatorStore.MeasurementHandler;

        // 5 recordings <=10
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, -10D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 0D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 1D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 9D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 10D, tags: default);

        // 2 recordings >10, <=20
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 11D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, 19D, tags: default);

        measurementHandler.CollectMeasurementsOnMetricPoint(ref histogramPoint);

        var count = histogramPoint.GetHistogramCount();
        var sum = histogramPoint.GetHistogramSum();

        // Sum of all recordings
        Assert.Equal(40, sum);

        // Count  = # of recordings
        Assert.Equal(7, count);

        int index = 0;
        int actualCount = 0;
        var expectedBucketCounts = new long[] { 5, 2, 0 };
        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }

        Assert.Equal(boundaries.Length + 1, actualCount);
    }

    [Fact]
    public void HistogramBinaryBucketTest()
    {
        // Arrange
        // Bounds = (-Inf, 0] (0, 1], ... (49, +Inf)
        var boundaries = new double[HistogramBuckets.DefaultBoundaryCountForBinarySearch];
        for (var i = 0; i < boundaries.Length; i++)
        {
            boundaries[i] = i;
        }

        var histogramPoint = new MetricPoint(this.aggregatorStore,  null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets, Metric.DefaultExponentialHistogramMaxScale);

        var measurementHandler = this.aggregatorStore.MeasurementHandler;

        // Act
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, -1D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, boundaries[0], tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, boundaries[boundaries.Length - 1], tags: default);
        for (var i = 0.5; i < boundaries.Length; i++)
        {
            measurementHandler.RecordMeasurementOnMetricPoint(this.aggregatorStore, ref histogramPoint, i, tags: default);
        }

        measurementHandler.CollectMeasurementsOnMetricPoint(ref histogramPoint);

        // Assert
        var index = 0;
        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            var expectedCount = 1;

            if (index == 0 || index == boundaries.Length - 1)
            {
                expectedCount = 2;
            }

            Assert.Equal(expectedCount, histogramMeasurement.BucketCount);
            index++;
        }
    }

    [Fact]
    public void HistogramWithOnlySumCount()
    {
        var boundaries = Array.Empty<double>();

        AggregatorStore aggregatorStore = new(
            MetricStreamIdentity,
            MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithoutBuckets,
            AggregationTemporality.Cumulative,
            maxMetricPoints: 1024,
            this.emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints);

        var histogramPoint = new MetricPoint(aggregatorStore, null, boundaries, Metric.DefaultExponentialHistogramMaxBuckets, Metric.DefaultExponentialHistogramMaxScale);

        var measurementHandler = aggregatorStore.MeasurementHandler;

        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, -10D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 0D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 1D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 9D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 10D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 11D, tags: default);
        measurementHandler.RecordMeasurementOnMetricPoint(aggregatorStore, ref histogramPoint, 19D, tags: default);

        measurementHandler.CollectMeasurementsOnMetricPoint(ref histogramPoint);

        var count = histogramPoint.GetHistogramCount();
        var sum = histogramPoint.GetHistogramSum();

        // Sum of all recordings
        Assert.Equal(40, sum);

        // Count  = # of recordings
        Assert.Equal(7, count);

        // There should be no enumeration of BucketCounts and ExplicitBounds for HistogramSumCount
        var enumerator = histogramPoint.GetHistogramBuckets().GetEnumerator();
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(AggregationTemporality.Cumulative)]
    [InlineData(AggregationTemporality.Delta)]
    public void MultiThreadedHistogramUpdateAndSnapShotTest(AggregationTemporality aggregationTemporality)
    {
        var boundaries = Array.Empty<double>();

        AggregatorStore aggregatorStore = new(
            MetricStreamIdentity,
            MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithoutBuckets,
            aggregationTemporality,
            maxMetricPoints: 1024,
            this.emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints);

        var metricPointIndex = aggregatorStore.FindMetricPointIndexDefault(tags: default);

        var argsToThread = new ThreadArguments
        {
            AggregatorStore = aggregatorStore,
            HistogramPoint = aggregatorStore.GetMetricPoint(metricPointIndex),
            MreToEnsureAllThreadsStart = new ManualResetEvent(false),
        };

        var numberOfThreads = 2;
        var snapshotThread = new Thread(HistogramSnapshotThread);
        Thread[] updateThreads = new Thread[numberOfThreads];
        for (int i = 0; i < numberOfThreads; ++i)
        {
            updateThreads[i] = new Thread(HistogramUpdateThread);
            updateThreads[i].Start(argsToThread);
        }

        snapshotThread.Start(argsToThread);

        for (int i = 0; i < numberOfThreads; ++i)
        {
            updateThreads[i].Join();
        }

        snapshotThread.Join();

        // last snapshot
        aggregatorStore.MeasurementHandler.CollectMeasurementsOnMetricPoint(ref argsToThread.HistogramPoint);

        var lastSum = argsToThread.HistogramPoint.GetHistogramSum();
        if (aggregationTemporality == AggregationTemporality.Cumulative)
        {
            Assert.Equal(200, lastSum);
        }
        else
        {
            Assert.Equal(200, argsToThread.SumOfDelta + lastSum);
        }
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting", "http.server.request.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("Microsoft.AspNetCore.Hosting", "http.server.request.duration", "ms", KnownHistogramBuckets.Default)]
    [InlineData("Microsoft.AspNetCore.Hosting", "http.server.request.duration", "By", KnownHistogramBuckets.Default)]
    [InlineData("Microsoft.AspNetCore.Hosting", "http.server.request.duration", null, KnownHistogramBuckets.Default)]
    [InlineData("Microsoft.AspNetCore.Http.Connections", "signalr.server.connection.duration", "s", KnownHistogramBuckets.DefaultLongSeconds)]
    [InlineData("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request_lease.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request.time_in_queue", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel", "kestrel.connection.duration", "s", KnownHistogramBuckets.DefaultLongSeconds)]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel", "kestrel.tls_handshake.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("OpenTelemetry.Instrumentation.AspNet", "http.server.duration", "ms", KnownHistogramBuckets.Default)]
    [InlineData("OpenTelemetry.Instrumentation.AspNet", "http.server.request.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("OpenTelemetry.Instrumentation.AspNetCore", "http.server.duration", "ms", KnownHistogramBuckets.Default)]
    [InlineData("OpenTelemetry.Instrumentation.Http", "http.client.duration", "ms", KnownHistogramBuckets.Default)]
    [InlineData("System.Net.Http", "http.client.connection.duration", "s", KnownHistogramBuckets.DefaultLongSeconds)]
    [InlineData("System.Net.Http", "http.client.request.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("System.Net.Http", "http.client.request.time_in_queue", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("System.Net.NameResolution", "dns.lookup.duration", "s", KnownHistogramBuckets.DefaultShortSeconds)]
    [InlineData("General.App", "simple.alternative.counter", "s", KnownHistogramBuckets.Default)]
    public void HistogramBucketsDefaultUpdatesForSecondsTest(string meterName, string instrumentName, string unit, KnownHistogramBuckets expectedHistogramBuckets)
    {
        using var meter = new Meter(meterName);

        var instrument = meter.CreateHistogram<double>(instrumentName, unit);

        var metricStreamIdentity = new MetricStreamIdentity(instrument, metricStreamConfiguration: null);

        AggregatorStore aggregatorStore = new(
            metricStreamIdentity,
            MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithoutBuckets,
            AggregationTemporality.Cumulative,
            maxMetricPoints: 1024,
            this.emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints);

        KnownHistogramBuckets actualHistogramBounds = KnownHistogramBuckets.Default;
        if (aggregatorStore.HistogramBounds == Metric.DefaultHistogramBoundsShortSeconds)
        {
            actualHistogramBounds = KnownHistogramBuckets.DefaultShortSeconds;
        }
        else if (aggregatorStore.HistogramBounds == Metric.DefaultHistogramBoundsLongSeconds)
        {
            actualHistogramBounds = KnownHistogramBuckets.DefaultLongSeconds;
        }

        Assert.NotNull(aggregatorStore.HistogramBounds);
        Assert.Equal(expectedHistogramBuckets, actualHistogramBounds);
    }

    internal static void AssertExponentialBucketsAreCorrect(Base2ExponentialBucketHistogram expectedHistogram, ExponentialHistogramData data)
    {
        Assert.Equal(expectedHistogram.Scale, data.Scale);
        Assert.Equal(expectedHistogram.ZeroCount, data.ZeroCount);
        Assert.Equal(expectedHistogram.PositiveBuckets.Offset, data.PositiveBuckets.Offset);
        Assert.Equal(expectedHistogram.NegativeBuckets.Offset, data.NegativeBuckets.Offset);

        expectedHistogram.Snapshot();
        var expectedData = expectedHistogram.GetExponentialHistogramData();

        var actual = new List<long>();
        foreach (var bucketCount in data.PositiveBuckets)
        {
            actual.Add(bucketCount);
        }

        var expected = new List<long>();
        foreach (var bucketCount in expectedData.PositiveBuckets)
        {
            expected.Add(bucketCount);
        }

        Assert.Equal(expected, actual);

        actual = new List<long>();
        foreach (var bucketCount in data.NegativeBuckets)
        {
            actual.Add(bucketCount);
        }

        expected = new List<long>();
        foreach (var bucketCount in expectedData.NegativeBuckets)
        {
            expected.Add(bucketCount);
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets, AggregationTemporality.Cumulative, true)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets, AggregationTemporality.Delta, true)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets | MetricPointBehaviors.HistogramRecordMinMax, AggregationTemporality.Cumulative, true)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets | MetricPointBehaviors.HistogramRecordMinMax, AggregationTemporality.Delta, true)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets, AggregationTemporality.Cumulative, false)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets, AggregationTemporality.Delta, false)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets | MetricPointBehaviors.HistogramRecordMinMax, AggregationTemporality.Cumulative, false)]
    [InlineData(MetricPointBehaviors.HistogramWithExponentialBuckets | MetricPointBehaviors.HistogramRecordMinMax, AggregationTemporality.Delta, false)]
    internal void ExponentialHistogramTests(MetricPointBehaviors metricBehaviors, AggregationTemporality aggregationTemporality, bool exemplarsEnabled)
    {
        var valuesToRecord = new double[] { -10, 0, 1, 9, 10, 11, 19 };

        var streamConfiguration = new Base2ExponentialBucketHistogramConfiguration();
        var metricStreamIdentity = new MetricStreamIdentity(Instrument, streamConfiguration);

        metricBehaviors |= MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithExponentialBuckets;

        var aggregatorStore = new AggregatorStore(
            metricStreamIdentity,
            metricBehaviors,
            aggregationTemporality,
            maxMetricPoints: 1024,
            this.emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints,
            exemplarsEnabled ? new AlwaysOnExemplarFilter() : null);

        var measurementHandler = aggregatorStore.MeasurementHandler;

        var expectedHistogram = new Base2ExponentialBucketHistogram();

        foreach (var value in valuesToRecord)
        {
            measurementHandler.RecordMeasurement(aggregatorStore, value, tags: default);

            if (value >= 0)
            {
                expectedHistogram.Record(value);
            }
        }

        measurementHandler.CollectMeasurements(aggregatorStore);

        var metricPoints = new List<MetricPoint>();

        foreach (ref readonly var mp in aggregatorStore.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();
        var hasMinMax = metricPoint.TryGetHistogramMinMaxValues(out var min, out var max);

        AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
        Assert.Equal(50, sum);
        Assert.Equal(6, count);

        if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramRecordMinMax))
        {
            Assert.True(hasMinMax);
            Assert.Equal(0, min);
            Assert.Equal(19, max);
        }
        else
        {
            Assert.False(hasMinMax);
        }

        measurementHandler.CollectMeasurementsOnMetricPoint(ref metricPoint);

        count = metricPoint.GetHistogramCount();
        sum = metricPoint.GetHistogramSum();
        hasMinMax = metricPoint.TryGetHistogramMinMaxValues(out min, out max);

        if (aggregationTemporality == AggregationTemporality.Cumulative)
        {
            AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
            Assert.Equal(50, sum);
            Assert.Equal(6, count);

            if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramRecordMinMax))
            {
                Assert.True(hasMinMax);
                Assert.Equal(0, min);
                Assert.Equal(19, max);
            }
            else
            {
                Assert.False(hasMinMax);
            }
        }
        else
        {
            expectedHistogram.Reset();
            AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
            Assert.Equal(0, sum);
            Assert.Equal(0, count);

            if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramRecordMinMax))
            {
                Assert.True(hasMinMax);
                Assert.Equal(double.PositiveInfinity, min);
                Assert.Equal(double.NegativeInfinity, max);
            }
            else
            {
                Assert.False(hasMinMax);
            }
        }
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(null)]
    internal void ExponentialMaxScaleConfigWorks(int? maxScale)
    {
        var streamConfiguration = new Base2ExponentialBucketHistogramConfiguration();
        if (maxScale.HasValue)
        {
            streamConfiguration.MaxScale = maxScale.Value;
        }

        var metricStreamIdentity = new MetricStreamIdentity(Instrument, streamConfiguration);

        var aggregatorStore = new AggregatorStore(
            metricStreamIdentity,
            MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithExponentialBuckets,
            AggregationTemporality.Cumulative,
            maxMetricPoints: 1024,
            this.emitOverflowAttribute,
            this.shouldReclaimUnusedMetricPoints);

        var measurementHandler = aggregatorStore.MeasurementHandler;

        measurementHandler.RecordMeasurement(aggregatorStore, 10D, tags: default);

        measurementHandler.CollectMeasurements(aggregatorStore);

        var metricPoints = new List<MetricPoint>();

        foreach (ref readonly var mp in aggregatorStore.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];

        // After a single measurement there will not have been a scale down.
        // Scale will equal MaxScale.
        var expectedScale = maxScale.HasValue ? maxScale : Metric.DefaultExponentialHistogramMaxScale;
        Assert.Equal(expectedScale, metricPoint.GetExponentialHistogramData().Scale);
    }

    private static void HistogramSnapshotThread(object obj)
    {
        var args = obj as ThreadArguments;
        var mreToEnsureAllThreadsStart = args.MreToEnsureAllThreadsStart;

        if (Interlocked.Increment(ref args.ThreadStartedCount) == 3)
        {
            mreToEnsureAllThreadsStart.Set();
        }

        mreToEnsureAllThreadsStart.WaitOne();

        double curSnapshotDelta;
        while (Interlocked.Read(ref args.ThreadsFinishedAllUpdatesCount) != 2)
        {
            args.AggregatorStore.MeasurementHandler.CollectMeasurementsOnMetricPoint(ref args.HistogramPoint);
            curSnapshotDelta = args.HistogramPoint.GetHistogramSum();
            args.SumOfDelta += curSnapshotDelta;
        }
    }

    private static void HistogramUpdateThread(object obj)
    {
        var args = obj as ThreadArguments;
        var mreToEnsureAllThreadsStart = args.MreToEnsureAllThreadsStart;

        if (Interlocked.Increment(ref args.ThreadStartedCount) == 3)
        {
            mreToEnsureAllThreadsStart.Set();
        }

        mreToEnsureAllThreadsStart.WaitOne();

        for (int i = 0; i < 10; ++i)
        {
            args.AggregatorStore.MeasurementHandler.RecordMeasurementOnMetricPoint(args.AggregatorStore, ref args.HistogramPoint, 10D, tags: default);
        }

        Interlocked.Increment(ref args.ThreadsFinishedAllUpdatesCount);
    }

    private class ThreadArguments
    {
        public AggregatorStore AggregatorStore;
        public MetricPoint HistogramPoint;
        public ManualResetEvent MreToEnsureAllThreadsStart;
        public int ThreadStartedCount;
        public long ThreadsFinishedAllUpdatesCount;
        public double SumOfDelta;
    }
}

public class AggregatorTests : AggregatorTestsBase
{
    public AggregatorTests()
        : base(emitOverflowAttribute: false, shouldReclaimUnusedMetricPoints: false)
    {
    }
}

public class AggregatorTestsWithOverflowAttribute : AggregatorTestsBase
{
    public AggregatorTestsWithOverflowAttribute()
        : base(emitOverflowAttribute: true, shouldReclaimUnusedMetricPoints: false)
    {
    }
}

public class AggregatorTestsWithReclaimAttribute : AggregatorTestsBase
{
    public AggregatorTestsWithReclaimAttribute()
        : base(emitOverflowAttribute: false, shouldReclaimUnusedMetricPoints: true)
    {
    }
}

public class AggregatorTestsWithBothReclaimAndOverflowAttributes : AggregatorTestsBase
{
    public AggregatorTestsWithBothReclaimAndOverflowAttributes()
        : base(emitOverflowAttribute: true, shouldReclaimUnusedMetricPoints: true)
    {
    }
}
