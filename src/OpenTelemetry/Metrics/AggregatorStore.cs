// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class AggregatorStore
{
    internal readonly HashSet<string>? TagKeysInteresting;
    internal readonly bool OutputDelta;
    internal readonly bool OutputDeltaWithUnusedMetricPointReclaimEnabled;
    internal readonly int CardinalityLimit;
    internal readonly bool EmitOverflowAttribute;
    internal readonly ConcurrentDictionary<Tags, LookupData>? TagsToMetricPointIndexDictionaryDelta;
    internal readonly Func<ExemplarReservoir?>? ExemplarReservoirFactory;
    internal long DroppedMeasurements = 0;

    private static readonly string MetricPointCapHitFixMessage = "Consider opting in for the experimental SDK feature to emit all the throttled metrics under the overflow attribute by setting env variable OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE = true. You could also modify instrumentation to reduce the number of unique key/value pair combinations. Or use Views to drop unwanted tags. Or use MeterProviderBuilder.SetMaxMetricPointsPerMetricStream to set higher limit.";
    private static readonly Comparison<KeyValuePair<string, object?>> DimensionComparisonDelegate = (x, y) => x.Key.CompareTo(y.Key);
    private static readonly ExemplarFilter DefaultExemplarFilter = new AlwaysOffExemplarFilter();

    private readonly object lockZeroTags = new();
    private readonly object lockOverflowTag = new();
    private readonly int tagsKeysInterestingCount;

    // This holds the reclaimed MetricPoints that are available for reuse.
    private readonly Queue<int>? availableMetricPoints;

    private readonly ConcurrentDictionary<Tags, int> tagsToMetricPointIndexDictionary =
        new();

    private readonly string name;
    private readonly string metricPointCapHitMessage;
    private readonly MetricPoint[] metricPoints;
    private readonly int[] currentMetricPointBatch;
    private readonly AggregationType aggType;
    private readonly double[] histogramBounds;
    private readonly int exponentialHistogramMaxSize;
    private readonly int exponentialHistogramMaxScale;
    private readonly UpdateLongDelegate updateLongCallback;
    private readonly UpdateDoubleDelegate updateDoubleCallback;
    private readonly ExemplarFilter exemplarFilter;
    private readonly Func<KeyValuePair<string, object?>[], int, int> lookupAggregatorStore;

    private int metricPointIndex = 0;
    private int batchSize = 0;
    private int metricCapHitMessageLogged;
    private bool zeroTagMetricPointInitialized;
    private bool overflowTagMetricPointInitialized;

    internal AggregatorStore(
        MetricStreamIdentity metricStreamIdentity,
        AggregationType aggType,
        AggregationTemporality temporality,
        int cardinalityLimit,
        bool emitOverflowAttribute,
        bool shouldReclaimUnusedMetricPoints,
        ExemplarFilter? exemplarFilter = null,
        Func<ExemplarReservoir?>? exemplarReservoirFactory = null)
    {
        this.name = metricStreamIdentity.InstrumentName;
        this.CardinalityLimit = cardinalityLimit;

        this.metricPointCapHitMessage = $"Maximum MetricPoints limit reached for this Metric stream. Configured limit: {this.CardinalityLimit}";
        this.metricPoints = new MetricPoint[cardinalityLimit];
        this.currentMetricPointBatch = new int[cardinalityLimit];
        this.aggType = aggType;
        this.OutputDelta = temporality == AggregationTemporality.Delta;
        this.histogramBounds = metricStreamIdentity.HistogramBucketBounds ?? FindDefaultHistogramBounds(in metricStreamIdentity);
        this.exponentialHistogramMaxSize = metricStreamIdentity.ExponentialHistogramMaxSize;
        this.exponentialHistogramMaxScale = metricStreamIdentity.ExponentialHistogramMaxScale;
        this.StartTimeExclusive = DateTimeOffset.UtcNow;
        this.exemplarFilter = exemplarFilter ?? DefaultExemplarFilter;
        this.ExemplarReservoirFactory = exemplarReservoirFactory;
        if (metricStreamIdentity.TagKeys == null)
        {
            this.updateLongCallback = this.UpdateLong;
            this.updateDoubleCallback = this.UpdateDouble;
        }
        else
        {
            this.updateLongCallback = this.UpdateLongCustomTags;
            this.updateDoubleCallback = this.UpdateDoubleCustomTags;
            var hs = new HashSet<string>(metricStreamIdentity.TagKeys, StringComparer.Ordinal);
            this.TagKeysInteresting = hs;
            this.tagsKeysInterestingCount = hs.Count;
        }

        this.EmitOverflowAttribute = emitOverflowAttribute;

        var reservedMetricPointsCount = 1;

        if (emitOverflowAttribute)
        {
            // Setting metricPointIndex to 1 as we would reserve the metricPoints[1] for overflow attribute.
            // Newer attributes should be added starting at the index: 2
            this.metricPointIndex = 1;
            reservedMetricPointsCount++;
        }

        this.OutputDeltaWithUnusedMetricPointReclaimEnabled = shouldReclaimUnusedMetricPoints && this.OutputDelta;

        if (this.OutputDeltaWithUnusedMetricPointReclaimEnabled)
        {
            this.availableMetricPoints = new Queue<int>(cardinalityLimit - reservedMetricPointsCount);

            // There is no overload which only takes capacity as the parameter
            // Using the DefaultConcurrencyLevel defined in the ConcurrentDictionary class: https://github.com/dotnet/runtime/blob/v7.0.5/src/libraries/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentDictionary.cs#L2020
            // We expect at the most (maxMetricPoints - reservedMetricPointsCount) * 2 entries- one for sorted and one for unsorted input
            this.TagsToMetricPointIndexDictionaryDelta =
                new ConcurrentDictionary<Tags, LookupData>(concurrencyLevel: Environment.ProcessorCount, capacity: (cardinalityLimit - reservedMetricPointsCount) * 2);

            // Add all the indices except for the reserved ones to the queue so that threads have
            // readily available access to these MetricPoints for their use.
            for (int i = reservedMetricPointsCount; i < this.CardinalityLimit; i++)
            {
                this.availableMetricPoints.Enqueue(i);
            }

            this.lookupAggregatorStore = this.LookupAggregatorStoreForDeltaWithReclaim;
        }
        else
        {
            this.lookupAggregatorStore = this.LookupAggregatorStore;
        }
    }

    private delegate void UpdateLongDelegate(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    private delegate void UpdateDoubleDelegate(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    internal DateTimeOffset StartTimeExclusive { get; private set; }

    internal DateTimeOffset EndTimeInclusive { get; private set; }

    internal double[] HistogramBounds => this.histogramBounds;

    internal bool IsExemplarEnabled()
    {
        // Using this filter to indicate On/Off
        // instead of another separate flag.
        return this.exemplarFilter is not AlwaysOffExemplarFilter;
    }

    internal void Update(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.updateLongCallback(value, tags);
    }

    internal void Update(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.updateDoubleCallback(value, tags);
    }

    internal int Snapshot()
    {
        this.batchSize = 0;
        if (this.OutputDeltaWithUnusedMetricPointReclaimEnabled)
        {
            this.SnapshotDeltaWithMetricPointReclaim();
        }
        else if (this.OutputDelta)
        {
            var indexSnapshot = Math.Min(this.metricPointIndex, this.CardinalityLimit - 1);
            this.SnapshotDelta(indexSnapshot);
        }
        else
        {
            var indexSnapshot = Math.Min(this.metricPointIndex, this.CardinalityLimit - 1);
            this.SnapshotCumulative(indexSnapshot);
        }

        this.EndTimeInclusive = DateTimeOffset.UtcNow;
        return this.batchSize;
    }

    internal void SnapshotDelta(int indexSnapshot)
    {
        for (int i = 0; i <= indexSnapshot; i++)
        {
            ref var metricPoint = ref this.metricPoints[i];
            if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
            {
                continue;
            }

            if (this.IsExemplarEnabled())
            {
                metricPoint.TakeSnapshotWithExemplar(outputDelta: true);
            }
            else
            {
                metricPoint.TakeSnapshot(outputDelta: true);
            }

            this.currentMetricPointBatch[this.batchSize] = i;
            this.batchSize++;
        }

        if (this.EndTimeInclusive != default)
        {
            this.StartTimeExclusive = this.EndTimeInclusive;
        }
    }

    internal void SnapshotDeltaWithMetricPointReclaim()
    {
        // Index = 0 is reserved for the case where no dimensions are provided.
        ref var metricPointWithNoTags = ref this.metricPoints[0];
        if (metricPointWithNoTags.MetricPointStatus != MetricPointStatus.NoCollectPending)
        {
            if (this.IsExemplarEnabled())
            {
                metricPointWithNoTags.TakeSnapshotWithExemplar(outputDelta: true);
            }
            else
            {
                metricPointWithNoTags.TakeSnapshot(outputDelta: true);
            }

            this.currentMetricPointBatch[this.batchSize] = 0;
            this.batchSize++;
        }

        int startIndexForReclaimableMetricPoints = 1;

        if (this.EmitOverflowAttribute)
        {
            startIndexForReclaimableMetricPoints = 2; // Index 0 and 1 are reserved for no tags and overflow

            // TakeSnapshot for the MetricPoint for overflow
            ref var metricPointForOverflow = ref this.metricPoints[1];
            if (metricPointForOverflow.MetricPointStatus != MetricPointStatus.NoCollectPending)
            {
                if (this.IsExemplarEnabled())
                {
                    metricPointForOverflow.TakeSnapshotWithExemplar(outputDelta: true);
                }
                else
                {
                    metricPointForOverflow.TakeSnapshot(outputDelta: true);
                }

                this.currentMetricPointBatch[this.batchSize] = 1;
                this.batchSize++;
            }
        }

        for (int i = startIndexForReclaimableMetricPoints; i < this.CardinalityLimit; i++)
        {
            ref var metricPoint = ref this.metricPoints[i];

            if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
            {
                // If metricPoint.LookupData is `null` then the MetricPoint is already reclaimed and in the queue.
                // If the Collect thread is successfully able to compare and swap the reference count from zero to int.MinValue, it means that
                // the MetricPoint can be reused for other tags.
                if (metricPoint.LookupData != null && Interlocked.CompareExchange(ref metricPoint.ReferenceCount, int.MinValue, 0) == 0)
                {
                    var lookupData = metricPoint.LookupData;

                    // Setting `LookupData` to `null` to denote that this MetricPoint is reclaimed.
                    // Snapshot method can use this to skip trying to reclaim indices which have already been reclaimed and added to the queue.
                    metricPoint.LookupData = null;

                    Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta was null");

                    lock (this.TagsToMetricPointIndexDictionaryDelta!)
                    {
                        LookupData? dictionaryValue;
                        if (lookupData.SortedTags != Tags.EmptyTags)
                        {
                            // Check if no other thread added a new entry for the same Tags.
                            // If no, then remove the existing entries.
                            if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue) &&
                                dictionaryValue == lookupData)
                            {
                                this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out var _);
                                this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                            }
                        }
                        else
                        {
                            if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue) &&
                                dictionaryValue == lookupData)
                            {
                                this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                            }
                        }

                        Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints was null");

                        this.availableMetricPoints!.Enqueue(i);
                    }
                }

                continue;
            }

            if (this.IsExemplarEnabled())
            {
                metricPoint.TakeSnapshotWithExemplar(outputDelta: true);
            }
            else
            {
                metricPoint.TakeSnapshot(outputDelta: true);
            }

            this.currentMetricPointBatch[this.batchSize] = i;
            this.batchSize++;
        }

        if (this.EndTimeInclusive != default)
        {
            this.StartTimeExclusive = this.EndTimeInclusive;
        }
    }

    internal void SnapshotCumulative(int indexSnapshot)
    {
        for (int i = 0; i <= indexSnapshot; i++)
        {
            ref var metricPoint = ref this.metricPoints[i];
            if (!metricPoint.IsInitialized)
            {
                continue;
            }

            if (this.IsExemplarEnabled())
            {
                metricPoint.TakeSnapshotWithExemplar(outputDelta: false);
            }
            else
            {
                metricPoint.TakeSnapshot(outputDelta: false);
            }

            this.currentMetricPointBatch[this.batchSize] = i;
            this.batchSize++;
        }
    }

    internal MetricPointsAccessor GetMetricPoints()
        => new(this.metricPoints, this.currentMetricPointBatch, this.batchSize);

    private static double[] FindDefaultHistogramBounds(in MetricStreamIdentity metricStreamIdentity)
    {
        if (metricStreamIdentity.Unit == "s")
        {
            if (Metric.DefaultHistogramBoundShortMappings
                .Contains((metricStreamIdentity.MeterName, metricStreamIdentity.InstrumentName)))
            {
                return Metric.DefaultHistogramBoundsShortSeconds;
            }

            if (Metric.DefaultHistogramBoundLongMappings
                .Contains((metricStreamIdentity.MeterName, metricStreamIdentity.InstrumentName)))
            {
                return Metric.DefaultHistogramBoundsLongSeconds;
            }
        }

        return Metric.DefaultHistogramBounds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeZeroTagPointIfNotInitialized()
    {
        if (!this.zeroTagMetricPointInitialized)
        {
            lock (this.lockZeroTags)
            {
                if (!this.zeroTagMetricPointInitialized)
                {
                    if (this.OutputDelta)
                    {
                        var lookupData = new LookupData(0, Tags.EmptyTags, Tags.EmptyTags);
                        this.metricPoints[0] = new MetricPoint(this, this.aggType, null, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                    }
                    else
                    {
                        this.metricPoints[0] = new MetricPoint(this, this.aggType, null, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);
                    }

                    this.zeroTagMetricPointInitialized = true;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeOverflowTagPointIfNotInitialized()
    {
        if (!this.overflowTagMetricPointInitialized)
        {
            lock (this.lockOverflowTag)
            {
                if (!this.overflowTagMetricPointInitialized)
                {
                    var keyValuePairs = new KeyValuePair<string, object?>[] { new("otel.metric.overflow", true) };
                    var tags = new Tags(keyValuePairs);

                    if (this.OutputDelta)
                    {
                        var lookupData = new LookupData(1, tags, tags);
                        this.metricPoints[1] = new MetricPoint(this, this.aggType, keyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                    }
                    else
                    {
                        this.metricPoints[1] = new MetricPoint(this, this.aggType, keyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);
                    }

                    this.overflowTagMetricPointInitialized = true;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LookupAggregatorStore(KeyValuePair<string, object?>[] tagKeysAndValues, int length)
    {
        var givenTags = new Tags(tagKeysAndValues);

        if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out var aggregatorIndex))
        {
            if (length > 1)
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                // Create or obtain new arrays to temporarily hold the sorted tag Keys and Values
                var storage = ThreadStaticStorage.GetStorage();
                storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                var sortedTags = new Tags(tempSortedTagKeysAndValues);

                if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
                {
                    aggregatorIndex = this.metricPointIndex;
                    if (aggregatorIndex >= this.CardinalityLimit)
                    {
                        // sorry! out of data points.
                        // TODO: Once we support cleanup of
                        // unused points (typically with delta)
                        // we can re-claim them here.
                        return -1;
                    }

                    // Note: We are using storage from ThreadStatic (for upto MaxTagCacheSize tags) for both the input order of tags and the sorted order of tags,
                    // so we need to make a deep copy for Dictionary storage.
                    if (length <= ThreadStaticStorage.MaxTagCacheSize)
                    {
                        var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                        var sortedTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                        givenTags = new Tags(givenTagKeysAndValues);
                        sortedTags = new Tags(sortedTagKeysAndValues);
                    }

                    lock (this.tagsToMetricPointIndexDictionary)
                    {
                        // check again after acquiring lock.
                        if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
                        {
                            aggregatorIndex = ++this.metricPointIndex;
                            if (aggregatorIndex >= this.CardinalityLimit)
                            {
                                // sorry! out of data points.
                                // TODO: Once we support cleanup of
                                // unused points (typically with delta)
                                // we can re-claim them here.
                                return -1;
                            }

                            ref var metricPoint = ref this.metricPoints[aggregatorIndex];
                            metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);

                            // Add to dictionary *after* initializing MetricPoint
                            // as other threads can start writing to the
                            // MetricPoint, if dictionary entry found.

                            // Add the sorted order along with the given order of tags
                            this.tagsToMetricPointIndexDictionary.TryAdd(sortedTags, aggregatorIndex);
                            this.tagsToMetricPointIndexDictionary.TryAdd(givenTags, aggregatorIndex);
                        }
                    }
                }
            }
            else
            {
                // This else block is for tag length = 1
                aggregatorIndex = this.metricPointIndex;
                if (aggregatorIndex >= this.CardinalityLimit)
                {
                    // sorry! out of data points.
                    // TODO: Once we support cleanup of
                    // unused points (typically with delta)
                    // we can re-claim them here.
                    return -1;
                }

                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];

                tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                givenTags = new Tags(givenTagKeysAndValues);

                lock (this.tagsToMetricPointIndexDictionary)
                {
                    // check again after acquiring lock.
                    if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out aggregatorIndex))
                    {
                        aggregatorIndex = ++this.metricPointIndex;
                        if (aggregatorIndex >= this.CardinalityLimit)
                        {
                            // sorry! out of data points.
                            // TODO: Once we support cleanup of
                            // unused points (typically with delta)
                            // we can re-claim them here.
                            return -1;
                        }

                        ref var metricPoint = ref this.metricPoints[aggregatorIndex];
                        metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);

                        // Add to dictionary *after* initializing MetricPoint
                        // as other threads can start writing to the
                        // MetricPoint, if dictionary entry found.

                        // givenTags will always be sorted when tags length == 1
                        this.tagsToMetricPointIndexDictionary.TryAdd(givenTags, aggregatorIndex);
                    }
                }
            }
        }

        return aggregatorIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LookupAggregatorStoreForDeltaWithReclaim(KeyValuePair<string, object?>[] tagKeysAndValues, int length)
    {
        int index;
        var givenTags = new Tags(tagKeysAndValues);

        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta was null");

        bool newMetricPointCreated = false;

        if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out var lookupData))
        {
            if (length > 1)
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                // Create or obtain new arrays to temporarily hold the sorted tag Keys and Values
                var storage = ThreadStaticStorage.GetStorage();
                storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                var sortedTags = new Tags(tempSortedTagKeysAndValues);

                if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                {
                    // Note: We are using storage from ThreadStatic (for up to MaxTagCacheSize tags) for both the input order of tags and the sorted order of tags,
                    // so we need to make a deep copy for Dictionary storage.
                    if (length <= ThreadStaticStorage.MaxTagCacheSize)
                    {
                        var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                        var sortedTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                        givenTags = new Tags(givenTagKeysAndValues);
                        sortedTags = new Tags(sortedTagKeysAndValues);
                    }

                    Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints was null");

                    lock (this.TagsToMetricPointIndexDictionaryDelta)
                    {
                        // check again after acquiring lock.
                        if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                        {
                            // Check for an available MetricPoint
                            if (this.availableMetricPoints!.Count > 0)
                            {
                                index = this.availableMetricPoints.Dequeue();
                            }
                            else
                            {
                                // No MetricPoint is available for reuse
                                return -1;
                            }

                            lookupData = new LookupData(index, sortedTags, givenTags);

                            ref var metricPoint = ref this.metricPoints[index];
                            metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                            newMetricPointCreated = true;

                            // Add to dictionary *after* initializing MetricPoint
                            // as other threads can start writing to the
                            // MetricPoint, if dictionary entry found.

                            // Add the sorted order along with the given order of tags
                            this.TagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                            this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                        }
                    }
                }
            }
            else
            {
                // This else block is for tag length = 1

                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];

                tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                givenTags = new Tags(givenTagKeysAndValues);

                Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints was null");

                lock (this.TagsToMetricPointIndexDictionaryDelta)
                {
                    // check again after acquiring lock.
                    if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out lookupData))
                    {
                        // Check for an available MetricPoint
                        if (this.availableMetricPoints!.Count > 0)
                        {
                            index = this.availableMetricPoints.Dequeue();
                        }
                        else
                        {
                            // No MetricPoint is available for reuse
                            return -1;
                        }

                        lookupData = new LookupData(index, Tags.EmptyTags, givenTags);

                        ref var metricPoint = ref this.metricPoints[index];
                        metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                        newMetricPointCreated = true;

                        // Add to dictionary *after* initializing MetricPoint
                        // as other threads can start writing to the
                        // MetricPoint, if dictionary entry found.

                        // givenTags will always be sorted when tags length == 1
                        this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                    }
                }
            }
        }

        // Found the MetricPoint
        index = lookupData.Index;

        // If the running thread created a new MetricPoint, then the Snapshot method cannot reclaim that MetricPoint because MetricPoint is initialized with a ReferenceCount of 1.
        // It can simply return the index.

        if (!newMetricPointCreated)
        {
            // If the running thread did not create the MetricPoint, it could be working on an index that has been reclaimed by Snapshot method.
            // This could happen if the thread get switched out by CPU after it retrieves the index but the Snapshot method reclaims it before the thread wakes up again.

            ref var metricPointAtIndex = ref this.metricPoints[index];
            var referenceCount = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);

            if (referenceCount < 0)
            {
                // Rare case: Snapshot method had already marked the MetricPoint available for reuse as it has not been updated in last collect cycle.

                // Example scenario:
                // Thread T1 wants to record a measurement for (k1,v1).
                // Thread T1 creates a new MetricPoint at index 100 and adds an entry for (k1,v1) in the dictionary with the relevant LookupData value; ReferenceCount of the MetricPoint is 1 at this point.
                // Thread T1 completes the update and decrements the ReferenceCount to 0.
                // Later, another update thread (could be T1 as well) wants to record a measurement for (k1,v1)
                // It looks up the dictionary and retrieves the index as 100. ReferenceCount for the MetricPoint is 0 at this point.
                // This update thread gets switched out by the CPU.
                // With the reclaim behavior, Snapshot method reclaims the index 100 as the MetricPoint for the index has NoCollectPending and has a ReferenceCount of 0.
                // Snapshot thread sets the ReferenceCount to int.MinValue.
                // The update thread wakes up and increments the ReferenceCount but finds the value to be negative.

                // Retry attempt to get a MetricPoint.
                index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
            }
            else if (metricPointAtIndex.LookupData != lookupData)
            {
                // Rare case: Another thread with different input tags could have reclaimed this MetricPoint if it was freed up by Snapshot method.

                // Example scenario:
                // Thread T1 wants to record a measurement for (k1,v1).
                // Thread T1 creates a new MetricPoint at index 100 and adds an entry for (k1,v1) in the dictionary with the relevant LookupData value; ReferenceCount of the MetricPoint is 1 at this point.
                // Thread T1 completes the update and decrements the ReferenceCount to 0.
                // Later, another update thread T2 (could be T1 as well) wants to record a measurement for (k1,v1)
                // It looks up the dictionary and retrieves the index as 100. ReferenceCount for the MetricPoint is 0 at this point.
                // This update thread T2 gets switched out by the CPU.
                // With the reclaim behavior, Snapshot method reclaims the index 100 as the MetricPoint for the index has NoCollectPending and has a ReferenceCount of 0.
                // Snapshot thread sets the ReferenceCount to int.MinValue.
                // An update thread T3 wants to record a measurement for (k2,v2).
                // Thread T3 looks for an available index from the queue and finds index 100.
                // Thread T3 creates a new MetricPoint at index 100 and adds an entry for (k2,v2) in the dictionary with the LookupData value for (k2,v2). ReferenceCount of the MetricPoint is 1 at this point.
                // The update thread T2 wakes up and increments the ReferenceCount and finds the value to be positive but the LookupData value does not match the one for (k1,v1).

                // Remove reference since its not the right MetricPoint.
                Interlocked.Decrement(ref metricPointAtIndex.ReferenceCount);

                // Retry attempt to get a MetricPoint.
                index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
            }
        }

        return index;
    }

    // This method is always called under `lock(this.tagsToMetricPointIndexDictionaryDelta)` so it's safe with other code that adds or removes
    // entries from `this.tagsToMetricPointIndexDictionaryDelta`
    private bool TryGetAvailableMetricPointRare(
        Tags givenTags,
        Tags sortedTags,
        int length,
        [NotNullWhen(true)]
        out LookupData? lookupData,
        out bool newMetricPointCreated)
    {
        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta was null");
        Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints was null");

        int index;
        newMetricPointCreated = false;

        if (length > 1)
        {
            // check again after acquiring lock.
            if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out lookupData) &&
                !this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
            {
                // Check for an available MetricPoint
                if (this.availableMetricPoints!.Count > 0)
                {
                    index = this.availableMetricPoints.Dequeue();
                }
                else
                {
                    // No MetricPoint is available for reuse
                    return false;
                }

                lookupData = new LookupData(index, sortedTags, givenTags);

                ref var metricPoint = ref this.metricPoints[index];
                metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                newMetricPointCreated = true;

                // Add to dictionary *after* initializing MetricPoint
                // as other threads can start writing to the
                // MetricPoint, if dictionary entry found.

                // Add the sorted order along with the given order of tags
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
            }
        }
        else
        {
            // check again after acquiring lock.
            if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out lookupData))
            {
                // Check for an available MetricPoint
                if (this.availableMetricPoints!.Count > 0)
                {
                    index = this.availableMetricPoints.Dequeue();
                }
                else
                {
                    // No MetricPoint is available for reuse
                    return false;
                }

                lookupData = new LookupData(index, Tags.EmptyTags, givenTags);

                ref var metricPoint = ref this.metricPoints[index];
                metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                newMetricPointCreated = true;

                // Add to dictionary *after* initializing MetricPoint
                // as other threads can start writing to the
                // MetricPoint, if dictionary entry found.

                // givenTags will always be sorted when tags length == 1
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
            }
        }

        return true;
    }

    // This method is essentially a retry attempt for when `LookupAggregatorStoreForDeltaWithReclaim` cannot find a MetricPoint.
    // If we still fail to get a MetricPoint in this method, we don't retry any further and simply drop the measurement.
    // This method acquires `lock (this.tagsToMetricPointIndexDictionaryDelta)`
    private int RemoveStaleEntriesAndGetAvailableMetricPointRare(LookupData lookupData, int length)
    {
        bool foundMetricPoint = false;
        bool newMetricPointCreated = false;
        var sortedTags = lookupData.SortedTags;
        var inputTags = lookupData.GivenTags;

        // Acquire lock
        // Try to remove stale entries from dictionary
        // Get the index for a new MetricPoint (it could be self-claimed or from another thread that added a fresh entry)
        // If self-claimed, then add a fresh entry to the dictionary
        // If an available MetricPoint is found, then only increment the ReferenceCount

        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta was null");

        // Delete the entry for these Tags and get another MetricPoint.
        lock (this.TagsToMetricPointIndexDictionaryDelta!)
        {
            LookupData? dictionaryValue;
            if (lookupData.SortedTags != Tags.EmptyTags)
            {
                // Check if no other thread added a new entry for the same Tags in the meantime.
                // If no, then remove the existing entries.
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue))
                {
                    if (dictionaryValue == lookupData)
                    {
                        // No other thread added a new entry for the same Tags.
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out _);
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
                    }
                    else
                    {
                        // Some other thread added a new entry for these Tags. Use the new MetricPoint
                        lookupData = dictionaryValue;
                        foundMetricPoint = true;
                    }
                }
            }
            else
            {
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue))
                {
                    if (dictionaryValue == lookupData)
                    {
                        // No other thread added a new entry for the same Tags.
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
                    }
                    else
                    {
                        // Some other thread added a new entry for these Tags. Use the new MetricPoint
                        lookupData = dictionaryValue;
                        foundMetricPoint = true;
                    }
                }
            }

            if (!foundMetricPoint
                && this.TryGetAvailableMetricPointRare(inputTags, sortedTags, length, out var tempLookupData, out newMetricPointCreated))
            {
                foundMetricPoint = true;
                lookupData = tempLookupData;
            }
        }

        if (foundMetricPoint)
        {
            var index = lookupData.Index;

            // If the running thread created a new MetricPoint, then the Snapshot method cannot reclaim that MetricPoint because MetricPoint is initialized with a ReferenceCount of 1.
            // It can simply return the index.

            if (!newMetricPointCreated)
            {
                // If the running thread did not create the MetricPoint, it could be working on an index that has been reclaimed by Snapshot method.
                // This could happen if the thread get switched out by CPU after it retrieves the index but the Snapshot method reclaims it before the thread wakes up again.

                ref var metricPointAtIndex = ref this.metricPoints[index];
                var referenceCount = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);

                if (referenceCount < 0)
                {
                    // Super rare case: Snapshot method had already marked the MetricPoint available for reuse as it has not been updated in last collect cycle even in the retry attempt.
                    // Example scenario mentioned in `LookupAggregatorStoreForDeltaWithReclaim` method.

                    // Don't retry again and drop the measurement.
                    return -1;
                }
                else if (metricPointAtIndex.LookupData != lookupData)
                {
                    // Rare case: Another thread with different input tags could have reclaimed this MetricPoint if it was freed up by Snapshot method even in the retry attempt.
                    // Example scenario mentioned in `LookupAggregatorStoreForDeltaWithReclaim` method.

                    // Remove reference since its not the right MetricPoint.
                    Interlocked.Decrement(ref metricPointAtIndex.ReferenceCount);

                    // Don't retry again and drop the measurement.
                    return -1;
                }
            }

            return index;
        }
        else
        {
            // No MetricPoint is available for reuse
            return -1;
        }
    }

    private void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var index = this.FindMetricAggregatorsDefault(tags);
            if (index < 0)
            {
                Interlocked.Increment(ref this.DroppedMeasurements);

                if (this.EmitOverflowAttribute)
                {
                    this.InitializeOverflowTagPointIfNotInitialized();
                    this.metricPoints[1].Update(value);
                    return;
                }
                else
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    return;
                }
            }

            // TODO: can special case built-in filters to be bit faster.
            if (this.IsExemplarEnabled())
            {
                var shouldSample = this.exemplarFilter.ShouldSample(value, tags);
                this.metricPoints[index].UpdateWithExemplar(value, tags: default, shouldSample);
            }
            else
            {
                this.metricPoints[index].Update(value);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.DroppedMeasurements);
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    private void UpdateLongCustomTags(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var index = this.FindMetricAggregatorsCustomTag(tags);
            if (index < 0)
            {
                Interlocked.Increment(ref this.DroppedMeasurements);

                if (this.EmitOverflowAttribute)
                {
                    this.InitializeOverflowTagPointIfNotInitialized();
                    this.metricPoints[1].Update(value);
                    return;
                }
                else
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    return;
                }
            }

            // TODO: can special case built-in filters to be bit faster.
            if (this.IsExemplarEnabled())
            {
                var shouldSample = this.exemplarFilter.ShouldSample(value, tags);
                this.metricPoints[index].UpdateWithExemplar(value, tags: tags, shouldSample);
            }
            else
            {
                this.metricPoints[index].Update(value);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.DroppedMeasurements);
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    private void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var index = this.FindMetricAggregatorsDefault(tags);
            if (index < 0)
            {
                Interlocked.Increment(ref this.DroppedMeasurements);

                if (this.EmitOverflowAttribute)
                {
                    this.InitializeOverflowTagPointIfNotInitialized();
                    this.metricPoints[1].Update(value);
                    return;
                }
                else
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    return;
                }
            }

            // TODO: can special case built-in filters to be bit faster.
            if (this.IsExemplarEnabled())
            {
                var shouldSample = this.exemplarFilter.ShouldSample(value, tags);
                this.metricPoints[index].UpdateWithExemplar(value, tags: default, shouldSample);
            }
            else
            {
                this.metricPoints[index].Update(value);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.DroppedMeasurements);
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    private void UpdateDoubleCustomTags(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var index = this.FindMetricAggregatorsCustomTag(tags);
            if (index < 0)
            {
                Interlocked.Increment(ref this.DroppedMeasurements);

                if (this.EmitOverflowAttribute)
                {
                    this.InitializeOverflowTagPointIfNotInitialized();
                    this.metricPoints[1].Update(value);
                    return;
                }
                else
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    return;
                }
            }

            // TODO: can special case built-in filters to be bit faster.
            if (this.IsExemplarEnabled())
            {
                var shouldSample = this.exemplarFilter.ShouldSample(value, tags);
                this.metricPoints[index].UpdateWithExemplar(value, tags: tags, shouldSample);
            }
            else
            {
                this.metricPoints[index].Update(value);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.DroppedMeasurements);
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    private int FindMetricAggregatorsDefault(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        int tagLength = tags.Length;
        if (tagLength == 0)
        {
            this.InitializeZeroTagPointIfNotInitialized();
            return 0;
        }

        var storage = ThreadStaticStorage.GetStorage();

        storage.SplitToKeysAndValues(tags, tagLength, out var tagKeysAndValues);

        return this.lookupAggregatorStore(tagKeysAndValues, tagLength);
    }

    private int FindMetricAggregatorsCustomTag(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        int tagLength = tags.Length;
        if (tagLength == 0 || this.tagsKeysInterestingCount == 0)
        {
            this.InitializeZeroTagPointIfNotInitialized();
            return 0;
        }

        var storage = ThreadStaticStorage.GetStorage();

        Debug.Assert(this.TagKeysInteresting != null, "this.tagKeysInteresting was null");

        storage.SplitToKeysAndValues(tags, tagLength, this.TagKeysInteresting!, out var tagKeysAndValues, out var actualLength);

        // Actual number of tags depend on how many
        // of the incoming tags has user opted to
        // select.
        if (actualLength == 0)
        {
            this.InitializeZeroTagPointIfNotInitialized();
            return 0;
        }

        Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues was null");

        return this.lookupAggregatorStore(tagKeysAndValues!, actualLength);
    }
}
