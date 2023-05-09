// <copyright file="AggregatorStore.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    internal sealed class AggregatorStore
    {
        internal readonly bool OutputDelta;
        internal long DroppedMeasurements = 0;

        private static readonly string MetricPointCapHitFixMessage = "Modify instrumentation to reduce the number of unique key/value pair combinations. Or use Views to drop unwanted tags. Or use MeterProviderBuilder.SetMaxMetricPointsPerMetricStream to set higher limit.";
        private static readonly Comparison<KeyValuePair<string, object>> DimensionComparisonDelegate = (x, y) => x.Key.CompareTo(y.Key);

        private readonly object lockZeroTags = new();
        private readonly HashSet<string> tagKeysInteresting;
        private readonly int tagsKeysInterestingCount;

        // This only applies to Delta AggregationTemporality.
        // This decides when to change the behavior to start reclaiming MetricPoints.
        // It is set to maxMetricPoints * 3 / 4, which means that Snapshot method would start to reclaim MetricPoints
        // only after 75% of the MetricPoints have been used. Once the AggregatorStore starts to reclaim MetricPoints,
        // it will continue to do so on every Snapshot and it won't go back to its default behavior.
        private readonly int metricPointReclamationThreshold;

        // This holds the reclaimed MetricPoints that are available for reuse.
        private readonly Queue<int> availableMetricPoints;

        private readonly ConcurrentDictionary<Tags, int> tagsToMetricPointIndexDictionaryCumulative =
            new();

        private readonly ConcurrentDictionary<Tags, LookupData> tagsToMetricPointIndexDictionaryDelta;

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
        private readonly int maxMetricPoints;
        private readonly ExemplarFilter exemplarFilter;
        private readonly Func<KeyValuePair<string, object>[], int, int> lookupAggregatorStore;
        private int metricPointIndex = 0;
        private int batchSize = 0;
        private int metricCapHitMessageLogged;
        private bool zeroTagMetricPointInitialized;

        // When set to true, the behavior changes to reuse MetricPoints
        private bool reclaimMetricPoints = false;

        internal AggregatorStore(
            MetricStreamIdentity metricStreamIdentity,
            AggregationType aggType,
            AggregationTemporality temporality,
            int maxMetricPoints,
            ExemplarFilter exemplarFilter = null)
        {
            this.name = metricStreamIdentity.InstrumentName;
            this.maxMetricPoints = maxMetricPoints;

            this.availableMetricPoints = new Queue<int>(maxMetricPoints - 1);

            // There is no overload which only takes capacity as the parameter
            // Using the DefaultConcurrencyLevel defined in the ConcurrentDictionary class: https://github.com/dotnet/runtime/blob/v7.0.5/src/libraries/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentDictionary.cs#L2020
            // We expect at the most (maxMetricPoints - 1) * 2 entries- one for sorted and one for unsorted input
            this.tagsToMetricPointIndexDictionaryDelta =
                new ConcurrentDictionary<Tags, LookupData>(concurrencyLevel: Environment.ProcessorCount, capacity: (maxMetricPoints - 1) * 2);

            this.metricPointReclamationThreshold = 1;

            // this.metricPointReclamationThreshold = maxMetricPoints * 3 / 4;
            this.metricPointCapHitMessage = $"Maximum MetricPoints limit reached for this Metric stream. Configured limit: {this.maxMetricPoints}";
            this.metricPoints = new MetricPoint[maxMetricPoints];
            this.currentMetricPointBatch = new int[maxMetricPoints];
            this.aggType = aggType;
            this.OutputDelta = temporality == AggregationTemporality.Delta;
            this.histogramBounds = metricStreamIdentity.HistogramBucketBounds ?? Metric.DefaultHistogramBounds;
            this.exponentialHistogramMaxSize = metricStreamIdentity.ExponentialHistogramMaxSize;
            this.exponentialHistogramMaxScale = metricStreamIdentity.ExponentialHistogramMaxScale;
            this.StartTimeExclusive = DateTimeOffset.UtcNow;
            this.exemplarFilter = exemplarFilter ?? new AlwaysOffExemplarFilter();
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
                this.tagKeysInteresting = hs;
                this.tagsKeysInterestingCount = hs.Count;
            }

            if (this.OutputDelta)
            {
                this.lookupAggregatorStore = this.LookupAggregatorStoreForDelta;
            }
            else
            {
                this.lookupAggregatorStore = this.LookupAggregatorStoreForCumulative;
            }

            // Add the second half of MetricPoint indices to the queue so that threads have access to
            // readily available MetricPoints for their use.
            for (int i = this.metricPointReclamationThreshold + 1; i < this.maxMetricPoints; i++)
            {
                this.availableMetricPoints.Enqueue(i);
            }
        }

        private delegate void UpdateLongDelegate(long value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        private delegate void UpdateDoubleDelegate(double value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        internal DateTimeOffset StartTimeExclusive { get; private set; }

        internal DateTimeOffset EndTimeInclusive { get; private set; }

        internal bool IsExemplarEnabled()
        {
            // Using this filter to indicate On/Off
            // instead of another separate flag.
            return this.exemplarFilter is not AlwaysOffExemplarFilter;
        }

        internal void Update(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.updateLongCallback(value, tags);
        }

        internal void Update(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.updateDoubleCallback(value, tags);
        }

        internal int Snapshot()
        {
            this.batchSize = 0;
            if (this.OutputDelta)
            {
                if (this.reclaimMetricPoints)
                {
                    this.SnapshotDeltaWithMetricPointReclaim();
                }
                else
                {
                    var indexSnapshot = Math.Min(this.metricPointIndex, this.maxMetricPoints - 1);
                    this.SnapshotDelta(indexSnapshot);
                }
            }
            else
            {
                var indexSnapshot = Math.Min(this.metricPointIndex, this.maxMetricPoints - 1);
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
            // Index = 0 is a special case as it's not added to `tagsToMetricPointIndexDictionaryDelta` dictionary.
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

            for (int i = 1; i < this.maxMetricPoints; i++)
            {
                ref var metricPoint = ref this.metricPoints[i];

                if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
                {
                    // If metricPoint.LookupData is `null` then the MetricPoint is already reclaimed and in the queue.
                    // If the Collect thread is successfully able to compare and swap the reference count from zero to int.MinValue, it means that
                    // the MetricPoint can be reused for other tags.
                    // Index = 0 is reserved for the case where no dimensions are provided.
                    if (metricPoint.LookupData != null && Interlocked.CompareExchange(ref metricPoint.ReferenceCount, int.MinValue, 0) == 0)
                    {
                        var lookupData = metricPoint.LookupData;

                        // Setting `LookupData` to `null`. Another thread might try to use this MetricPoint for the existing Tags key as we still
                        // haven't removed the key from the dictionary. We set this to null, so that such a thread can check that `LookupData`
                        // value has changed and thereby confirm that the MetricPoint has been reclaimed.
                        metricPoint.LookupData = null;

                        lock (this.tagsToMetricPointIndexDictionaryDelta)
                        {
                            LookupData dictionaryValue;
                            if (lookupData.SortedTags != Tags.EmptyTags)
                            {
                                // Check if no other thread added a new entry for the same Tags.
                                // If no, then remove the existing entries.
                                if (this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue) &&
                                    dictionaryValue == lookupData)
                                {
                                    this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out var _);
                                    this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                                }
                            }
                            else
                            {
                                if (this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue) &&
                                dictionaryValue == lookupData)
                                {
                                    this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                                }
                            }

                            this.availableMetricPoints.Enqueue(i);
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
        private int LookupAggregatorStoreForCumulative(KeyValuePair<string, object>[] tagKeysAndValues, int length)
        {
            var givenTags = new Tags(tagKeysAndValues);

            if (!this.tagsToMetricPointIndexDictionaryCumulative.TryGetValue(givenTags, out var aggregatorIndex))
            {
                if (length > 1)
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    // Create or obtain new arrays to temporarily hold the sorted tag Keys and Values
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                    Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                    var sortedTags = new Tags(tempSortedTagKeysAndValues);

                    if (!this.tagsToMetricPointIndexDictionaryCumulative.TryGetValue(sortedTags, out aggregatorIndex))
                    {
                        aggregatorIndex = this.metricPointIndex;
                        if (aggregatorIndex >= this.maxMetricPoints)
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
                            var givenTagKeysAndValues = new KeyValuePair<string, object>[length];
                            tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                            var sortedTagKeysAndValues = new KeyValuePair<string, object>[length];
                            tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                            givenTags = new Tags(givenTagKeysAndValues);
                            sortedTags = new Tags(sortedTagKeysAndValues);
                        }

                        lock (this.tagsToMetricPointIndexDictionaryCumulative)
                        {
                            // check again after acquiring lock.
                            if (!this.tagsToMetricPointIndexDictionaryCumulative.TryGetValue(sortedTags, out aggregatorIndex))
                            {
                                aggregatorIndex = ++this.metricPointIndex;
                                if (aggregatorIndex >= this.maxMetricPoints)
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
                                this.tagsToMetricPointIndexDictionaryCumulative.TryAdd(sortedTags, aggregatorIndex);
                                this.tagsToMetricPointIndexDictionaryCumulative.TryAdd(givenTags, aggregatorIndex);
                            }
                        }
                    }
                }
                else
                {
                    // This else block is for tag length = 1
                    aggregatorIndex = this.metricPointIndex;
                    if (aggregatorIndex >= this.maxMetricPoints)
                    {
                        // sorry! out of data points.
                        // TODO: Once we support cleanup of
                        // unused points (typically with delta)
                        // we can re-claim them here.
                        return -1;
                    }

                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    var givenTagKeysAndValues = new KeyValuePair<string, object>[length];

                    tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                    givenTags = new Tags(givenTagKeysAndValues);

                    lock (this.tagsToMetricPointIndexDictionaryCumulative)
                    {
                        // check again after acquiring lock.
                        if (!this.tagsToMetricPointIndexDictionaryCumulative.TryGetValue(givenTags, out aggregatorIndex))
                        {
                            aggregatorIndex = ++this.metricPointIndex;
                            if (aggregatorIndex >= this.maxMetricPoints)
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
                            this.tagsToMetricPointIndexDictionaryCumulative.TryAdd(givenTags, aggregatorIndex);
                        }
                    }
                }
            }

            return aggregatorIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LookupAggregatorStoreForDelta(KeyValuePair<string, object>[] tagKeysAndValues, int length)
        {
            int index;
            var givenTags = new Tags(tagKeysAndValues);

            if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out var lookupData))
            {
                if (length > 1)
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    // Create or obtain new arrays to temporarily hold the sorted tag Keys and Values
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                    Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                    var sortedTags = new Tags(tempSortedTagKeysAndValues);

                    if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                    {
                        // Note: We are using storage from ThreadStatic (for upto MaxTagCacheSize tags) for both the input order of tags and the sorted order of tags,
                        // so we need to make a deep copy for Dictionary storage.
                        if (length <= ThreadStaticStorage.MaxTagCacheSize)
                        {
                            var givenTagKeysAndValues = new KeyValuePair<string, object>[length];
                            tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                            var sortedTagKeysAndValues = new KeyValuePair<string, object>[length];
                            tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                            givenTags = new Tags(givenTagKeysAndValues);
                            sortedTags = new Tags(sortedTagKeysAndValues);
                        }

                        lock (this.tagsToMetricPointIndexDictionaryDelta)
                        {
                            // check again after acquiring lock.
                            if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                            {
                                if (this.reclaimMetricPoints)
                                {
                                    // Check for an available MetricPoint
                                    if (this.availableMetricPoints.Count > 0)
                                    {
                                        index = this.availableMetricPoints.Dequeue();
                                    }
                                    else
                                    {
                                        // No MetricPoint is available for reuse
                                        return -1;
                                    }
                                }
                                else
                                {
                                    index = ++this.metricPointIndex;
                                    if (index == this.metricPointReclamationThreshold)
                                    {
                                        this.reclaimMetricPoints = true;
                                    }
                                }

                                lookupData = new LookupData(index, sortedTags, givenTags);

                                ref var metricPoint = ref this.metricPoints[index];
                                metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);

                                // Add to dictionary *after* initializing MetricPoint
                                // as other threads can start writing to the
                                // MetricPoint, if dictionary entry found.

                                // Add the sorted order along with the given order of tags
                                this.tagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                                this.tagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                            }
                        }
                    }
                }
                else
                {
                    // This else block is for tag length = 1

                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    var givenTagKeysAndValues = new KeyValuePair<string, object>[length];

                    tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                    givenTags = new Tags(givenTagKeysAndValues);

                    lock (this.tagsToMetricPointIndexDictionaryDelta)
                    {
                        // check again after acquiring lock.
                        if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out lookupData))
                        {
                            if (this.reclaimMetricPoints)
                            {
                                // Check for an available MetricPoint
                                if (this.availableMetricPoints.Count > 0)
                                {
                                    index = this.availableMetricPoints.Dequeue();
                                }
                                else
                                {
                                    // No MetricPoint is available for reuse
                                    return -1;
                                }
                            }
                            else
                            {
                                index = ++this.metricPointIndex;
                                if (index == this.metricPointReclamationThreshold)
                                {
                                    this.reclaimMetricPoints = true;
                                }
                            }

                            lookupData = new LookupData(index, Tags.EmptyTags, givenTags);

                            ref var metricPoint = ref this.metricPoints[index];
                            metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);

                            // Add to dictionary *after* initializing MetricPoint
                            // as other threads can start writing to the
                            // MetricPoint, if dictionary entry found.

                            // givenTags will always be sorted when tags length == 1
                            this.tagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                        }
                    }
                }
            }

            // Found the MetricPoint
            index = lookupData.Index;
            ref var metricPointAtIndex = ref this.metricPoints[index];
            var referenceCount = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);

            if (this.reclaimMetricPoints)
            {
                if (referenceCount < 0)
                {
                    // Rare case: TakeSnapsot method had already marked the MetricPoint available for reuse as it has not been updated in last collect cycle.

                    index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
                }
                else
                {
                    if (metricPointAtIndex.LookupData != lookupData)
                    {
                        // Rare case: Another thread with different input tags could have reclaimed this MetricPoint if it was freed up by TakeSnapsot method.

                        // Remove reference since its not the right MetricPoint.
                        Interlocked.Decrement(ref metricPointAtIndex.ReferenceCount);

                        index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
                    }
                }
            }

            return index;
        }

        // This method is always called under `lock(this.tagsToMetricPointIndexDictionaryDelta)` so it's safe with other code that adds or removes
        // entries from `this.tagsToMetricPointIndexDictionaryDelta`
        private bool TryGetAvailableMetricPointRare(Tags givenTags, Tags sortedTags, int length, out LookupData lookupData)
        {
            int index;
            if (length > 1)
            {
                // check again after acquiring lock.
                if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out lookupData) &&
                    !this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                {
                    // Check for an available MetricPoint
                    if (this.availableMetricPoints.Count > 0)
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

                    // Add to dictionary *after* initializing MetricPoint
                    // as other threads can start writing to the
                    // MetricPoint, if dictionary entry found.

                    // Add the sorted order along with the given order of tags
                    this.tagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                    this.tagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                }
            }
            else
            {
                // check again after acquiring lock.
                if (!this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out lookupData))
                {
                    // Check for an available MetricPoint
                    if (this.availableMetricPoints.Count > 0)
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

                    // Add to dictionary *after* initializing MetricPoint
                    // as other threads can start writing to the
                    // MetricPoint, if dictionary entry found.

                    // givenTags will always be sorted when tags length == 1
                    this.tagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                }
            }

            return true;
        }

        // This method acquires `lock (this.tagsToMetricPointIndexDictionaryDelta)`
        private int RemoveStaleEntriesAndGetAvailableMetricPointRare(LookupData lookupData, int length)
        {
            bool foundMetricPoint = false;
            var sortedTags = lookupData.SortedTags;
            var inputTags = lookupData.GivenTags;

            // Acquire lock
            // Try to remove stale entries from dictionary
            // Get the index for a new MetricPoint (it could be self-claimed or from another thread that added a fresh entry)
            // If self-claimed, then add a fresh entry to the dictionary
            // If an available MetricPoint is found, then increment the ReferenceCount

            // Delete the entry for these Tags and get another MetricPoint.
            lock (this.tagsToMetricPointIndexDictionaryDelta)
            {
                LookupData dictionaryValue;
                if (lookupData.SortedTags != Tags.EmptyTags)
                {
                    // Check if no other thread added a new entry for the same Tags.
                    // If no, then remove the existing entries.
                    if (this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue))
                    {
                        if (dictionaryValue == lookupData)
                        {
                            // No other thread added a new entry for the same Tags.
                            this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out _);
                            this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
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
                    if (this.tagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue))
                    {
                        if (dictionaryValue == lookupData)
                        {
                            // No other thread added a new entry for the same Tags.
                            this.tagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
                        }
                        else
                        {
                            // Some other thread added a new entry for these Tags. Use the new MetricPoint
                            lookupData = dictionaryValue;
                            foundMetricPoint = true;
                        }
                    }
                }

                if (!foundMetricPoint)
                {
                    foundMetricPoint = this.TryGetAvailableMetricPointRare(inputTags, sortedTags, length, out lookupData);
                }
            }

            if (foundMetricPoint)
            {
                var index = lookupData.Index;
                ref var metricPointAtIndex = ref this.metricPoints[index];
                _ = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);
                return index;
            }
            else
            {
                // No MetricPoint is available for reuse
                return -1;
            }
        }

        private void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregatorsDefault(tags);
                if (index < 0)
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    Interlocked.Increment(ref this.DroppedMeasurements);
                    return;
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

        private void UpdateLongCustomTags(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregatorsCustomTag(tags);
                if (index < 0)
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    Interlocked.Increment(ref this.DroppedMeasurements);
                    return;
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

        private void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregatorsDefault(tags);
                if (index < 0)
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    Interlocked.Increment(ref this.DroppedMeasurements);
                    return;
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

        private void UpdateDoubleCustomTags(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregatorsCustomTag(tags);
                if (index < 0)
                {
                    if (Interlocked.CompareExchange(ref this.metricCapHitMessageLogged, 1, 0) == 0)
                    {
                        OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, this.metricPointCapHitMessage, MetricPointCapHitFixMessage);
                    }

                    Interlocked.Increment(ref this.DroppedMeasurements);
                    return;
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

        private int FindMetricAggregatorsDefault(ReadOnlySpan<KeyValuePair<string, object>> tags)
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

        private int FindMetricAggregatorsCustomTag(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            int tagLength = tags.Length;
            if (tagLength == 0 || this.tagsKeysInterestingCount == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                return 0;
            }

            // TODO: Get only interesting tags
            // from the incoming tags

            var storage = ThreadStaticStorage.GetStorage();

            storage.SplitToKeysAndValues(tags, tagLength, this.tagKeysInteresting, out var tagKeysAndValues, out var actualLength);

            // Actual number of tags depend on how many
            // of the incoming tags has user opted to
            // select.
            if (actualLength == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                return 0;
            }

            return this.lookupAggregatorStore(tagKeysAndValues, actualLength);
        }
    }
}
