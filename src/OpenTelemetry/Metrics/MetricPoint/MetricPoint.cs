// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents a metric data point.
/// </summary>
public struct MetricPoint
{
    // Represents the number of update threads using this MetricPoint at any given point of time.
    // If the value is equal to int.MinValue which is -2147483648, it means that this MetricPoint is available for reuse.
    // We never increment the ReferenceCount for MetricPoint with no tags (index == 0) and the MetricPoint for overflow attribute,
    // but we always decrement it (in the Update methods). This should be fine.
    // ReferenceCount doesn't matter for MetricPoint with no tags and overflow attribute as they are never reclaimed.
    internal int ReferenceCount;

    private const int DefaultSimpleReservoirPoolSize = 1;

    private readonly AggregatorStore aggregatorStore;

    private readonly AggregationType aggType;

    private MetricPointOptionalComponents? mpComponents;

    // Represents temporality adjusted "value" for double/long metric types or "count" when histogram
    private MetricPointValueStorage runningValue;

    // Represents either "value" for double/long metric types or "count" when histogram
    private MetricPointValueStorage snapshotValue;

    private MetricPointValueStorage deltaLastValue;

    internal MetricPoint(
        AggregatorStore aggregatorStore,
        AggregationType aggType,
        KeyValuePair<string, object?>[]? tagKeysAndValues,
        HistogramExplicitBounds histogramExplicitBounds,
        int exponentialHistogramMaxSize,
        int exponentialHistogramMaxScale,
        LookupData? lookupData = null)
    {
        Debug.Assert(aggregatorStore != null, "AggregatorStore was null.");
        Debug.Assert(histogramExplicitBounds != null, "HistogramExplicitBounds was null.");
        Debug.Assert(!aggregatorStore!.OutputDelta || lookupData != null, "LookupData was null.");

        this.aggType = aggType;
        this.Tags = new ReadOnlyTagCollection(tagKeysAndValues);
        this.runningValue = default;
        this.snapshotValue = default;
        this.deltaLastValue = default;
        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
        this.ReferenceCount = 1;
        this.LookupData = lookupData;

        var isExemplarEnabled = aggregatorStore!.IsExemplarEnabled();

        ExemplarReservoir? reservoir;
        try
        {
            reservoir = isExemplarEnabled
                ? aggregatorStore.ExemplarReservoirFactory?.Invoke()
                : null;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricViewException("ExemplarReservoirFactory", ex);
            reservoir = null;
        }

        if (this.aggType == AggregationType.HistogramWithBuckets ||
            this.aggType == AggregationType.HistogramWithMinMaxBuckets)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.HistogramBuckets = new HistogramBuckets(histogramExplicitBounds);
            if (isExemplarEnabled && reservoir == null)
            {
                reservoir = new AlignedHistogramBucketExemplarReservoir(histogramExplicitBounds!.Bounds.Length);
            }
        }
        else if (this.aggType == AggregationType.Histogram ||
                 this.aggType == AggregationType.HistogramWithMinMax)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.HistogramBuckets = new HistogramBuckets(null);
        }
        else if (this.aggType == AggregationType.Base2ExponentialHistogram ||
            this.aggType == AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.Base2ExponentialBucketHistogram = new Base2ExponentialBucketHistogram(exponentialHistogramMaxSize, exponentialHistogramMaxScale);
            if (isExemplarEnabled && reservoir == null)
            {
                reservoir = new SimpleFixedSizeExemplarReservoir(Math.Min(20, exponentialHistogramMaxSize));
            }
        }
        else
        {
            this.mpComponents = null;
        }

        if (isExemplarEnabled && reservoir == null)
        {
            reservoir = new SimpleFixedSizeExemplarReservoir(DefaultSimpleReservoirPoolSize);
        }

        if (reservoir != null)
        {
            if (this.mpComponents == null)
            {
                this.mpComponents = new MetricPointOptionalComponents();
            }

            reservoir.Initialize(aggregatorStore);

            this.mpComponents.ExemplarReservoir = reservoir;
        }

        // Note: Intentionally set last because this is used to detect valid MetricPoints.
        this.aggregatorStore = aggregatorStore;
    }

    /// <summary>
    /// Gets the tags associated with the metric point.
    /// </summary>
    public readonly ReadOnlyTagCollection Tags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <summary>
    /// Gets the start time (UTC) associated with the metric point.
    /// </summary>
    public readonly DateTimeOffset StartTime => this.aggregatorStore.StartTimeExclusive;

    /// <summary>
    /// Gets the end time (UTC) associated with the metric point.
    /// </summary>
    public readonly DateTimeOffset EndTime => this.aggregatorStore.EndTimeInclusive;

    internal MetricPointStatus MetricPointStatus
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set;
    }

    // When the AggregatorStore is reclaiming MetricPoints, this serves the purpose of validating the a given thread is using the right
    // MetricPoint for update by checking it against what as added in the Dictionary. Also, when a thread finds out that the MetricPoint
    // that its using is already reclaimed, this helps avoid sorting of the tags for adding a new Dictionary entry.
    // Snapshot method can use this to skip trying to reclaim indices which have already been reclaimed and added to the queue.
    internal LookupData? LookupData { readonly get; private set; }

    internal readonly bool IsInitialized => this.aggregatorStore != null;

    /// <summary>
    /// Gets the sum long value associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.LongSum"/> metric type.
    /// </remarks>
    /// <returns>Long sum value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetSumLong()
    {
        if (this.aggType != AggregationType.LongSumIncomingDelta && this.aggType != AggregationType.LongSumIncomingCumulative)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumLong));
        }

        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// Gets the sum double value associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.DoubleSum"/> metric type.
    /// </remarks>
    /// <returns>Double sum value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetSumDouble()
    {
        if (this.aggType != AggregationType.DoubleSumIncomingDelta && this.aggType != AggregationType.DoubleSumIncomingCumulative)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumDouble));
        }

        return this.snapshotValue.AsDouble;
    }

    /// <summary>
    /// Gets the last long value of the gauge associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.LongGauge"/> metric type.
    /// </remarks>
    /// <returns>Long gauge value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetGaugeLastValueLong()
    {
        if (this.aggType != AggregationType.LongGauge)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueLong));
        }

        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// Gets the last double value of the gauge associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.DoubleGauge"/> metric type.
    /// </remarks>
    /// <returns>Double gauge value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetGaugeLastValueDouble()
    {
        if (this.aggType != AggregationType.DoubleGauge)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueDouble));
        }

        return this.snapshotValue.AsDouble;
    }

    /// <summary>
    /// Gets the count value of the histogram associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.Histogram"/> metric type.
    /// </remarks>
    /// <returns>Count value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetHistogramCount()
    {
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax &&
            this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramCount));
        }

        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// Gets the sum value of the histogram associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.Histogram"/> metric type.
    /// </remarks>
    /// <returns>Sum value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetHistogramSum()
    {
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax &&
            this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramSum));
        }

        Debug.Assert(
            this.mpComponents?.HistogramBuckets != null
            || this.mpComponents?.Base2ExponentialBucketHistogram != null,
            "HistogramBuckets and Base2ExponentialBucketHistogram were both null");

        return this.mpComponents!.HistogramBuckets != null
            ? this.mpComponents.HistogramBuckets.SnapshotSum
            : this.mpComponents.Base2ExponentialBucketHistogram!.SnapshotSum;
    }

    /// <summary>
    /// Gets the buckets of the histogram associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.Histogram"/> metric type.
    /// </remarks>
    /// <returns><see cref="HistogramBuckets"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly HistogramBuckets GetHistogramBuckets()
    {
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramBuckets));
        }

        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        return this.mpComponents!.HistogramBuckets!;
    }

    /// <summary>
    /// Gets the exponential histogram data associated with the metric point.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="MetricType.Histogram"/> metric type.
    /// </remarks>
    /// <returns><see cref="ExponentialHistogramData"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExponentialHistogramData GetExponentialHistogramData()
    {
        if (this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetExponentialHistogramData));
        }

        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        return this.mpComponents!.Base2ExponentialBucketHistogram!.GetExponentialHistogramData();
    }

    /// <summary>
    /// Gets the Histogram Min and Max values.
    /// </summary>
    /// <param name="min"> The histogram minimum value.</param>
    /// <param name="max"> The histogram maximum value.</param>
    /// <returns>True if minimum and maximum value exist, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetHistogramMinMaxValues(out double min, out double max)
    {
        if (this.aggType == AggregationType.HistogramWithMinMax
            || this.aggType == AggregationType.HistogramWithMinMaxBuckets)
        {
            Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

            min = this.mpComponents!.HistogramBuckets!.SnapshotMin;
            max = this.mpComponents.HistogramBuckets.SnapshotMax;
            return true;
        }

        if (this.aggType == AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

            min = this.mpComponents!.Base2ExponentialBucketHistogram!.SnapshotMin;
            max = this.mpComponents.Base2ExponentialBucketHistogram.SnapshotMax;
            return true;
        }

        min = 0;
        max = 0;
        return false;
    }

    /// <summary>
    /// Gets the exemplars associated with the metric point.
    /// </summary>
    /// <param name="exemplars"><see cref="ReadOnlyExemplarCollection"/>.</param>
    /// <returns><see langword="true" /> if exemplars exist; <see langword="false" /> otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetExemplars(out ReadOnlyExemplarCollection exemplars)
    {
        exemplars = this.mpComponents?.Exemplars ?? ReadOnlyExemplarCollection.Empty;
        return exemplars.MaximumCount > 0;
    }

    internal readonly MetricPoint Copy()
    {
        MetricPoint copy = this;
        copy.mpComponents = this.mpComponents?.Copy();
        return copy;
    }

    internal void Update(long number)
    {
        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
                {
                    Interlocked.Add(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.LongSumIncomingCumulative:
            case AggregationType.LongGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number);
                    return;
                }
        }

        this.CompleteUpdate();
    }

    internal void UpdateWithExemplar(long number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
                {
                    Interlocked.Add(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.LongSumIncomingCumulative:
            case AggregationType.LongGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }
        }

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    internal void Update(double number)
    {
        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    InterlockedHelper.Add(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
            case AggregationType.DoubleGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number);
                    return;
                }
        }

        this.CompleteUpdate();
    }

    internal void UpdateWithExemplar(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    InterlockedHelper.Add(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
            case AggregationType.DoubleGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }
        }

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    internal void TakeSnapshot(bool outputDelta)
    {
        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
            case AggregationType.LongSumIncomingCumulative:
                {
                    if (outputDelta)
                    {
                        long initValue = Interlocked.Read(ref this.runningValue.AsLong);
                        this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                        this.deltaLastValue.AsLong = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (initValue != Interlocked.Read(ref this.runningValue.AsLong))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
                    }

                    break;
                }

            case AggregationType.DoubleSumIncomingDelta:
            case AggregationType.DoubleSumIncomingCumulative:
                {
                    if (outputDelta)
                    {
                        double initValue = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        this.deltaLastValue.AsDouble = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (initValue != InterlockedHelper.Read(ref this.runningValue.AsDouble))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        this.snapshotValue.AsDouble = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                    }

                    break;
                }

            case AggregationType.LongGauge:
                {
                    this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (this.snapshotValue.AsLong != Interlocked.Read(ref this.runningValue.AsLong))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            case AggregationType.DoubleGauge:
                {
                    this.snapshotValue.AsDouble = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (this.snapshotValue.AsDouble != InterlockedHelper.Read(ref this.runningValue.AsDouble))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    histogramBuckets.Snapshot(outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Histogram:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    histogramBuckets.Snapshot(outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.Reset(isMinMax: false);
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

                    this.mpComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.Reset(isMinMax: true);
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    this.mpComponents.ReleaseLock();

                    break;
                }
        }
    }

    internal void TakeSnapshotWithExemplar(bool outputDelta)
    {
        Debug.Assert(this.mpComponents != null, "this.mpComponents was null");
        Debug.Assert(this.mpComponents!.ExemplarReservoir != null, "this.mpComponents.ExemplarReservoir was null");

        this.TakeSnapshot(outputDelta);

        this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir!.Collect();
    }

    /// <summary>
    /// This method sets the member object references of MetricPoint to `null`.
    /// This is done to have them collected faster by GC.
    /// </summary>
    internal void NullifyMetricPointState()
    {
        this.LookupData = null;
        this.mpComponents = null;
    }

    private void UpdateHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets!;

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
        }

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    private void UpdateHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets!;

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
        }

        histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
        histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    private void UpdateHistogramWithBuckets(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        int bucketIndex = histogramBuckets!.FindBucketIndex(number);

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
            histogramBuckets.BucketCounts[bucketIndex].RunningValue++;
        }

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar, bucketIndex);

        this.CompleteUpdate();
    }

    private void UpdateHistogramWithBucketsAndMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "histogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        int bucketIndex = histogramBuckets!.FindBucketIndex(number);

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
            histogramBuckets.BucketCounts[bucketIndex].RunningValue++;
        }

        histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
        histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar, bucketIndex);

        this.CompleteUpdate();
    }

    private void UpdateBase2ExponentialHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        if (number < 0)
        {
            this.CompleteUpdateWithoutMeasurement();
            return;
        }

        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogram.RunningSum += number;
            histogram.Record(number);
        }

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    private void UpdateBase2ExponentialHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        if (number < 0)
        {
            this.CompleteUpdateWithoutMeasurement();
            return;
        }

        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

        this.mpComponents.AcquireLock();

        unchecked
        {
            this.runningValue.AsLong++;
            histogram.RunningSum += number;
            histogram.Record(number);
        }

        histogram.RunningMin = Math.Min(histogram.RunningMin, number);
        histogram.RunningMax = Math.Max(histogram.RunningMax, number);

        this.mpComponents.ReleaseLock();

        this.UpdateExemplar(number, tags, offerExemplar);

        this.CompleteUpdate();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void UpdateExemplar(long number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        if (offerExemplar)
        {
            Debug.Assert(this.mpComponents?.ExemplarReservoir != null, "ExemplarReservoir was null");

            // TODO: A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
            this.mpComponents!.ExemplarReservoir!.Offer(
                new ExemplarMeasurement<long>(number, tags));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void UpdateExemplar(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar, int explicitBucketHistogramBucketIndex = -1)
    {
        if (offerExemplar)
        {
            Debug.Assert(this.mpComponents?.ExemplarReservoir != null, "ExemplarReservoir was null");

            // TODO: A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
            this.mpComponents!.ExemplarReservoir!.Offer(
                new ExemplarMeasurement<double>(number, tags, explicitBucketHistogramBucketIndex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteUpdate()
    {
        // There is a race with Snapshot:
        // Update() updates the value
        // Snapshot snapshots the value
        // Snapshot sets status to NoCollectPending
        // Update sets status to CollectPending -- this is not right as the Snapshot
        // already included the updated value.
        // In the absence of any new Update call until next Snapshot,
        // this results in exporting an Update even though
        // it had no update.
        // TODO: For Delta, this can be mitigated
        // by ignoring Zero points
        this.MetricPointStatus = MetricPointStatus.CollectPending;

        this.CompleteUpdateWithoutMeasurement();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteUpdateWithoutMeasurement()
    {
        if (this.aggregatorStore.OutputDelta)
        {
            Interlocked.Decrement(ref this.ReferenceCount);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotSupportedMetricTypeException(string methodName)
    {
        throw new NotSupportedException($"{methodName} is not supported for this metric type.");
    }
}
