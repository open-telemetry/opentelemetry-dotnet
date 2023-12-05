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
using System.Diagnostics.CodeAnalysis;
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

    internal MetricPointOptionalComponents? OptionalComponents;

    // Represents temporality adjusted "value" for double/long metric types or "count" when histogram
    internal MetricPointValueStorage RunningValue;

    private const int DefaultSimpleReservoirPoolSize = 1;

    private readonly AggregatorStore aggregatorStore;

    // Represents either "value" for double/long metric types or "count" when histogram
    private MetricPointValueStorage snapshotValue;

    private MetricPointValueStorage deltaLastValue;

    internal MetricPoint(
        AggregatorStore aggregatorStore,
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

        this.Tags = new ReadOnlyTagCollection(tagKeysAndValues);
        this.RunningValue = default;
        this.snapshotValue = default;
        this.deltaLastValue = default;
        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
        this.ReferenceCount = 1;
        this.LookupData = lookupData;

        var metricBehaviors = aggregatorStore.MetricBehaviors;

        ExemplarReservoir? reservoir = null;
        if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation))
        {
            this.OptionalComponents = new MetricPointOptionalComponents();

            if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramWithExponentialBuckets))
            {
                this.OptionalComponents.Base2ExponentialBucketHistogram = new Base2ExponentialBucketHistogram(exponentialHistogramMaxSize, exponentialHistogramMaxScale);
            }
            else if (!metricBehaviors.HasFlag(MetricPointBehaviors.HistogramWithoutBuckets))
            {
                this.OptionalComponents.HistogramBuckets = new HistogramBuckets(histogramExplicitBounds);
                if (aggregatorStore!.IsExemplarEnabled())
                {
                    reservoir = new AlignedHistogramBucketExemplarReservoir(histogramExplicitBounds!.Length);
                }
            }
            else
            {
                this.OptionalComponents.HistogramBuckets = new HistogramBuckets(null);
            }
        }

        if (aggregatorStore!.IsExemplarEnabled() && reservoir == null)
        {
            reservoir = new SimpleExemplarReservoir(DefaultSimpleReservoirPoolSize);
        }

        if (reservoir != null)
        {
            if (this.OptionalComponents == null)
            {
                this.OptionalComponents = new MetricPointOptionalComponents();
            }

            this.OptionalComponents.ExemplarReservoir = reservoir;
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
        set;
    }

    internal readonly MetricPointBehaviors MetricBehaviors => this.aggregatorStore.MetricBehaviors;

    internal readonly AggregationType AggregationType => this.aggregatorStore.AggregationType;

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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.Long | MetricPointBehaviors.Counter))
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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.Double | MetricPointBehaviors.Counter))
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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.Long | MetricPointBehaviors.Gauge))
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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.Double | MetricPointBehaviors.Gauge))
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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation))
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
        var metricBehaviors = this.MetricBehaviors;

        if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation))
        {
            Debug.Assert(this.OptionalComponents != null, "OptionalComponents was null");

            if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramWithExponentialBuckets))
            {
                Debug.Assert(
                    this.OptionalComponents!.Base2ExponentialBucketHistogram != null,
                    "Base2ExponentialBucketHistogram was null");

                return this.OptionalComponents!.Base2ExponentialBucketHistogram!.SnapshotSum;
            }
            else
            {
                Debug.Assert(
                    this.OptionalComponents!.HistogramBuckets != null,
                    "HistogramBuckets was null");

                return this.OptionalComponents!.HistogramBuckets!.SnapshotSum;
            }
        }
        else
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramSum));
            return 0; // Note: This should never be reached.
        }
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
        var metricBehaviors = this.MetricBehaviors;

        if (!metricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation)
            || metricBehaviors.HasFlag(MetricPointBehaviors.HistogramWithExponentialBuckets))
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramBuckets));
        }

        Debug.Assert(this.OptionalComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        return this.OptionalComponents!.HistogramBuckets!;
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
        if (!this.MetricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.HistogramWithExponentialBuckets))
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetExponentialHistogramData));
        }

        Debug.Assert(this.OptionalComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        return this.OptionalComponents!.Base2ExponentialBucketHistogram!.GetExponentialHistogramData();
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
        var metricBehaviors = this.MetricBehaviors;

        if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.HistogramRecordMinMax))
        {
            Debug.Assert(this.OptionalComponents != null, "OptionalComponents was null");

            if (metricBehaviors.HasFlag(MetricPointBehaviors.HistogramWithExponentialBuckets))
            {
                Debug.Assert(this.OptionalComponents!.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                var buckets = this.OptionalComponents!.Base2ExponentialBucketHistogram!;

                min = buckets.SnapshotMin;
                max = buckets.SnapshotMax;
                return true;
            }
            else
            {
                Debug.Assert(this.OptionalComponents!.HistogramBuckets != null, "HistogramBuckets was null");

                var buckets = this.OptionalComponents!.HistogramBuckets!;

                min = buckets.SnapshotMin;
                max = buckets.SnapshotMax;
                return true;
            }
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
        return this.OptionalComponents?.Exemplars ?? Array.Empty<Exemplar>();
    }

    internal readonly MetricPoint Copy()
    {
        MetricPoint copy = this;
        copy.OptionalComponents = this.OptionalComponents?.Copy();
        return copy;
    }

    internal void TakeSnapshot(bool outputDelta)
    {
        switch (this.AggregationType)
        {
            case AggregationType.LongSumIncomingDelta:
            case AggregationType.LongSumIncomingCumulative:
                {
                    if (outputDelta)
                    {
                        long initValue = Interlocked.Read(ref this.RunningValue.AsLong);
                        this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                        this.deltaLastValue.AsLong = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (initValue != Interlocked.Read(ref this.RunningValue.AsLong))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        this.snapshotValue.AsLong = Interlocked.Read(ref this.RunningValue.AsLong);
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
                        double initValue = Interlocked.CompareExchange(ref this.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        this.deltaLastValue.AsDouble = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // Check again if value got updated, if yes reset status.
                        // This ensures no Updates get Lost.
                        if (initValue != Interlocked.CompareExchange(ref this.RunningValue.AsDouble, 0.0, double.NegativeInfinity))
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
                        this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
                    }

                    break;
                }

            case AggregationType.LongGauge:
                {
                    this.snapshotValue.AsLong = Interlocked.Read(ref this.RunningValue.AsLong);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (this.snapshotValue.AsLong != Interlocked.Read(ref this.RunningValue.AsLong))
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
                    this.snapshotValue.AsDouble = Interlocked.CompareExchange(ref this.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (this.snapshotValue.AsDouble != Interlocked.CompareExchange(ref this.RunningValue.AsDouble, 0.0, double.NegativeInfinity))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    Debug.Assert(this.OptionalComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
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

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Histogram:
                {
                    Debug.Assert(this.OptionalComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    Debug.Assert(this.OptionalComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
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

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    Debug.Assert(this.OptionalComponents?.HistogramBuckets != null, "HistogramBuckets was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    Debug.Assert(this.OptionalComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogram = optionalComponents.Base2ExponentialBucketHistogram!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    Debug.Assert(this.OptionalComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var optionalComponents = this.OptionalComponents!;
                    var histogram = optionalComponents.Base2ExponentialBucketHistogram!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                        histogram.RunningMin = double.PositiveInfinity;
                        histogram.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }
        }
    }

    internal void TakeSnapshotWithExemplar(bool outputDelta)
    {
        Debug.Assert(this.OptionalComponents != null, "this.mpComponents was null");

        var optionalComponents = this.OptionalComponents!;

        switch (this.AggregationType)
        {
            case AggregationType.LongSumIncomingDelta:
            case AggregationType.LongSumIncomingCumulative:
                {
                    optionalComponents.AcquireLock();

                    if (outputDelta)
                    {
                        long initValue = this.RunningValue.AsLong;
                        this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                        this.deltaLastValue.AsLong = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    }
                    else
                    {
                        this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    }

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.DoubleSumIncomingDelta:
            case AggregationType.DoubleSumIncomingCumulative:
                {
                    optionalComponents.AcquireLock();

                    if (outputDelta)
                    {
                        double initValue = this.RunningValue.AsDouble;
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        this.deltaLastValue.AsDouble = initValue;
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
                    }
                    else
                    {
                        this.snapshotValue.AsDouble = this.RunningValue.AsDouble;
                    }

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.LongGauge:
                {
                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.DoubleGauge:
                {
                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsDouble = this.RunningValue.AsDouble;
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    Debug.Assert(optionalComponents.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
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

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Histogram:
                {
                    Debug.Assert(optionalComponents.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    Debug.Assert(optionalComponents.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
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

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    Debug.Assert(optionalComponents.HistogramBuckets != null, "HistogramBuckets was null");

                    var histogramBuckets = optionalComponents.HistogramBuckets!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    optionalComponents.Exemplars = optionalComponents.ExemplarReservoir?.Collect(this.Tags, outputDelta);

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    Debug.Assert(optionalComponents.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = optionalComponents.Base2ExponentialBucketHistogram!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    Debug.Assert(optionalComponents.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

                    var histogram = optionalComponents.Base2ExponentialBucketHistogram!;

                    optionalComponents.AcquireLock();

                    this.snapshotValue.AsLong = this.RunningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        this.RunningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                        histogram.RunningMin = double.PositiveInfinity;
                        histogram.RunningMax = double.NegativeInfinity;
                    }

                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    optionalComponents.ReleaseLock();

                    break;
                }
        }
    }

    [DoesNotReturn]
    private readonly void ThrowNotSupportedMetricTypeException(string methodName)
    {
        throw new NotSupportedException($"{methodName} is not supported for this metric type.");
    }
}
