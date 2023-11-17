// <copyright file="MetricPoint.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Runtime.CompilerServices;

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

    // When the AggregatorStore is reclaiming MetricPoints, this serves the purpose of validating the a given thread is using the right
    // MetricPoint for update by checking it against what as added in the Dictionary. Also, when a thread finds out that the MetricPoint
    // that its using is already reclaimed, this helps avoid sorting of the tags for adding a new Dictionary entry.
    internal LookupData? LookupData;

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
        double[] histogramExplicitBounds,
        int exponentialHistogramMaxSize,
        int exponentialHistogramMaxScale,
        LookupData? lookupData = null)
    {
        Debug.Assert(aggregatorStore != null, "AggregatorStore was null.");
        Debug.Assert(histogramExplicitBounds != null, "Histogram explicit Bounds was null.");

        if (aggregatorStore!.OutputDelta && aggregatorStore.ShouldReclaimUnusedMetricPoints)
        {
            Debug.Assert(lookupData != null, "LookupData was null.");
        }

        this.aggType = aggType;
        this.Tags = new ReadOnlyTagCollection(tagKeysAndValues);
        this.runningValue = default;
        this.snapshotValue = default;
        this.deltaLastValue = default;
        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
        this.ReferenceCount = 1;
        this.LookupData = lookupData;

        ExemplarReservoir? reservoir = null;
        if (this.aggType == AggregationType.HistogramWithBuckets ||
            this.aggType == AggregationType.HistogramWithMinMaxBuckets)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.HistogramBuckets = new HistogramBuckets(histogramExplicitBounds);
            if (aggregatorStore!.IsExemplarEnabled())
            {
                reservoir = new AlignedHistogramBucketExemplarReservoir(histogramExplicitBounds!.Length);
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
        }
        else
        {
            this.mpComponents = null;
        }

        if (aggregatorStore!.IsExemplarEnabled() && reservoir == null)
        {
            reservoir = new SimpleExemplarReservoir(DefaultSimpleReservoirPoolSize);
        }

        if (reservoir != null)
        {
            if (this.mpComponents == null)
            {
                this.mpComponents = new MetricPointOptionalComponents();
            }

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

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets the exemplars associated with the metric point.
    /// </summary>
    /// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
    /// <returns><see cref="Exemplar"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public
#else
    /// <summary>
    /// Gets the exemplars associated with the metric point.
    /// </summary>
    /// <returns><see cref="Exemplar"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal
#endif
        readonly Exemplar[] GetExemplars()
    {
        // TODO: Do not expose Exemplar data structure (array now)
        return this.mpComponents?.Exemplars ?? Array.Empty<Exemplar>();
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
                {
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.LongGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram((double)number);
                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax((double)number);
                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets((double)number);
                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax((double)number);
                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram((double)number);
                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax((double)number);
                    break;
                }
        }

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

        if (this.aggregatorStore.OutputDelta)
        {
            Interlocked.Decrement(ref this.ReferenceCount);
        }
    }

    internal void UpdateWithExemplar(long number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool isSampled)
    {
        Debug.Assert(this.mpComponents != null, "this.mpComponents was null");

        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    unchecked
                    {
                        this.runningValue.AsLong += number;
                    }

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.LongSumIncomingCumulative:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    this.runningValue.AsLong = number;

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.LongGauge:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    this.runningValue.AsLong = number;

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram((double)number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax((double)number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets((double)number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax((double)number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram((double)number, tags, true);
                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax((double)number, tags, true);
                    break;
                }
        }

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

        if (this.aggregatorStore.OutputDelta)
        {
            Interlocked.Decrement(ref this.ReferenceCount);
        }
    }

    internal void Update(double number)
    {
        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    double initValue, newValue;
                    var sw = default(SpinWait);
                    while (true)
                    {
                        initValue = this.runningValue.AsDouble;

                        unchecked
                        {
                            newValue = initValue + number;
                        }

                        if (initValue == Interlocked.CompareExchange(ref this.runningValue.AsDouble, newValue, initValue))
                        {
                            break;
                        }

                        sw.SpinOnce();
                    }

                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
                {
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.DoubleGauge:
                {
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number);
                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number);
                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number);
                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number);
                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number);
                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number);
                    break;
                }
        }

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

        if (this.aggregatorStore.OutputDelta)
        {
            Interlocked.Decrement(ref this.ReferenceCount);
        }
    }

    internal void UpdateWithExemplar(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool isSampled)
    {
        Debug.Assert(this.mpComponents != null, "this.mpComponents was null");

        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    unchecked
                    {
                        this.runningValue.AsDouble += number;
                    }

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    unchecked
                    {
                        this.runningValue.AsDouble = number;
                    }

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.DoubleGauge:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    unchecked
                    {
                        this.runningValue.AsDouble = number;
                    }

                    if (isSampled)
                    {
                        Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                        // TODO: Need to ensure that the lock is always released.
                        // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                        this.mpComponents.ExemplarReservoir!.Offer(number, tags);
                    }

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Histogram:
                {
                    this.UpdateHistogram(number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    this.UpdateHistogramWithMinMax(number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    this.UpdateHistogramWithBuckets(number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    this.UpdateHistogramWithBucketsAndMinMax(number, tags, reportExemplar: true, isSampled);
                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    this.UpdateBase2ExponentialHistogram(number, tags, true);
                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    this.UpdateBase2ExponentialHistogramWithMinMax(number, tags, true);
                    break;
                }
        }

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

        if (this.aggregatorStore.OutputDelta)
        {
            Interlocked.Decrement(ref this.ReferenceCount);
        }
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
                        // TODO:
                        // Is this thread-safe way to read double?
                        // As long as the value is not -ve infinity,
                        // the exchange (to 0.0) will never occur,
                        // but we get the original value atomically.
                        double initValue = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        this.deltaLastValue.AsDouble = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (initValue != Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        // TODO:
                        // Is this thread-safe way to read double?
                        // As long as the value is not -ve infinity,
                        // the exchange (to 0.0) will never occur,
                        // but we get the original value atomically.
                        this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
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
                    // TODO:
                    // Is this thread-safe way to read double?
                    // As long as the value is not -ve infinity,
                    // the exchange (to 0.0) will never occur,
                    // but we get the original value atomically.
                    this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (this.snapshotValue.AsDouble != Interlocked.CompareExchange(ref this.runningValue.AsDouble, 0.0, double.NegativeInfinity))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                    for (int i = 0; i < histogramBuckets.RunningBucketCounts!.Length; i++)
                    {
                        histogramBuckets.SnapshotBucketCounts[i] = histogramBuckets.RunningBucketCounts[i];
                        if (outputDelta)
                        {
                            histogramBuckets.RunningBucketCounts[i] = 0;
                        }
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Histogram:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

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

                    Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                    for (int i = 0; i < histogramBuckets.RunningBucketCounts!.Length; i++)
                    {
                        histogramBuckets.SnapshotBucketCounts[i] = histogramBuckets.RunningBucketCounts[i];
                        if (outputDelta)
                        {
                            histogramBuckets.RunningBucketCounts[i] = 0;
                        }
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

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

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

                    AcquireLock(ref histogram!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogram.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

                    AcquireLock(ref histogram!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                        histogram.RunningMin = double.PositiveInfinity;
                        histogram.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogram.IsCriticalSectionOccupied);

                    break;
                }
        }
    }

    internal void TakeSnapshotWithExemplar(bool outputDelta)
    {
        Debug.Assert(this.mpComponents != null, "this.mpComponents was null");

        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
            case AggregationType.LongSumIncomingCumulative:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    if (outputDelta)
                    {
                        long initValue = this.runningValue.AsLong;
                        this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                        this.deltaLastValue.AsLong = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    }
                    else
                    {
                        this.snapshotValue.AsLong = this.runningValue.AsLong;
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.DoubleSumIncomingDelta:
            case AggregationType.DoubleSumIncomingCumulative:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    if (outputDelta)
                    {
                        double initValue = this.runningValue.AsDouble;
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        this.deltaLastValue.AsDouble = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    }
                    else
                    {
                        this.snapshotValue.AsDouble = this.runningValue.AsDouble;
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.LongGauge:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.DoubleGauge:
                {
                    AcquireLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsDouble = this.runningValue.AsDouble;
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    ReleaseLock(ref this.mpComponents!.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    Debug.Assert(histogramBuckets != null, "histogramBuckets was null");

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                    for (int i = 0; i < histogramBuckets.RunningBucketCounts!.Length; i++)
                    {
                        histogramBuckets.SnapshotBucketCounts[i] = histogramBuckets.RunningBucketCounts[i];
                        if (outputDelta)
                        {
                            histogramBuckets.RunningBucketCounts[i] = 0;
                        }
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Histogram:
                {
                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    Debug.Assert(histogramBuckets != null, "histogramBuckets was null");

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    Debug.Assert(histogramBuckets != null, "histogramBuckets was null");

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

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

                    Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                    for (int i = 0; i < histogramBuckets.RunningBucketCounts!.Length; i++)
                    {
                        histogramBuckets.SnapshotBucketCounts[i] = histogramBuckets.RunningBucketCounts[i];
                        if (outputDelta)
                        {
                            histogramBuckets.RunningBucketCounts[i] = 0;
                        }
                    }

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    var histogramBuckets = this.mpComponents!.HistogramBuckets;

                    Debug.Assert(histogramBuckets != null, "histogramBuckets was null");

                    AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

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

                    this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

                    Debug.Assert(histogram != null, "histogram was null");

                    AcquireLock(ref histogram!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogram.IsCriticalSectionOccupied);

                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

                    Debug.Assert(histogram != null, "histogram was null");

                    AcquireLock(ref histogram!.IsCriticalSectionOccupied);

                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                        histogram.RunningMin = double.PositiveInfinity;
                        histogram.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    ReleaseLock(ref histogram.IsCriticalSectionOccupied);

                    break;
                }
        }
    }

    private static void AcquireLock(ref int isCriticalSectionOccupied)
    {
        var sw = default(SpinWait);
        while (Interlocked.Exchange(ref isCriticalSectionOccupied, 1) != 0)
        {
            sw.SpinOnce();
        }
    }

    private static void ReleaseLock(ref int isCriticalSectionOccupied)
    {
        Interlocked.Exchange(ref isCriticalSectionOccupied, 0);
    }

    private void UpdateHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false, bool isSampled = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
        }

        if (reportExemplar && isSampled)
        {
            Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

            // TODO: Need to ensure that the lock is always released.
            // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
            this.mpComponents.ExemplarReservoir!.Offer(number, tags);
        }

        ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);
    }

    private void UpdateHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false, bool isSampled = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        AcquireLock(ref histogramBuckets!.IsCriticalSectionOccupied);

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
            histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
            histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);
        }

        if (reportExemplar && isSampled)
        {
            Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

            // TODO: Need to ensure that the lock is always released.
            // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
            this.mpComponents.ExemplarReservoir!.Offer(number, tags);
        }

        ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);
    }

    private void UpdateHistogramWithBuckets(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false, bool isSampled = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        int i = histogramBuckets!.FindBucketIndex(number);

        Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

        AcquireLock(ref histogramBuckets.IsCriticalSectionOccupied);

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
            histogramBuckets.RunningBucketCounts![i]++;

            if (reportExemplar && isSampled)
            {
                Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                // TODO: Need to ensure that the lock is always released.
                // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                this.mpComponents.ExemplarReservoir!.Offer(number, tags, i);
            }
        }

        ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);
    }

    private void UpdateHistogramWithBucketsAndMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false, bool isSampled = false)
    {
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "histogramBuckets was null");

        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        int i = histogramBuckets!.FindBucketIndex(number);

        AcquireLock(ref histogramBuckets.IsCriticalSectionOccupied);

        Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

        unchecked
        {
            this.runningValue.AsLong++;
            histogramBuckets.RunningSum += number;
            histogramBuckets.RunningBucketCounts![i]++;

            if (reportExemplar && isSampled)
            {
                Debug.Assert(this.mpComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                // TODO: Need to ensure that the lock is always released.
                // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                this.mpComponents.ExemplarReservoir!.Offer(number, tags, i);
            }

            histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
            histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);
        }

        ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);
    }

#pragma warning disable IDE0060 // Remove unused parameter: Exemplars for exponential histograms will be a follow up PR
    private void UpdateBase2ExponentialHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (number < 0)
        {
            return;
        }

        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

        AcquireLock(ref histogram!.IsCriticalSectionOccupied);

        unchecked
        {
            this.runningValue.AsLong++;
            histogram.RunningSum += number;
            histogram.Record(number);
        }

        ReleaseLock(ref histogram.IsCriticalSectionOccupied);
    }

#pragma warning disable IDE0060 // Remove unused parameter: Exemplars for exponential histograms will be a follow up PR
    private void UpdateBase2ExponentialHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool reportExemplar = false)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (number < 0)
        {
            return;
        }

        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram;

        AcquireLock(ref histogram!.IsCriticalSectionOccupied);

        unchecked
        {
            this.runningValue.AsLong++;
            histogram.RunningSum += number;
            histogram.Record(number);

            histogram.RunningMin = Math.Min(histogram.RunningMin, number);
            histogram.RunningMax = Math.Max(histogram.RunningMax, number);
        }

        ReleaseLock(ref histogram.IsCriticalSectionOccupied);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotSupportedMetricTypeException(string methodName)
    {
        throw new NotSupportedException($"{methodName} is not supported for this metric type.");
    }
}
