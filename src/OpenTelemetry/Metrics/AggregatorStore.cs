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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    internal sealed class AggregatorStore
    {
        private static readonly string MetricPointCapHitFixMessage = "Modify instrumentation to reduce the number of unique key/value pair combinations. Or use Views to drop unwanted tags. Or use MeterProviderBuilder.SetMaxMetricPointsPerMetricStream to set higher limit.";
        private readonly object lockZeroTags = new();
        private readonly HashSet<string> tagKeysInteresting;
        private readonly int tagsKeysInterestingCount;

        private readonly ConcurrentDictionary<Tags, int> tagsToMetricPointIndexDictionary =
            new();

        private readonly string name;
        private readonly string metricPointCapHitMessage;
        private readonly bool outputDelta;
        private readonly MetricPoint[] metricPoints;
        private readonly int[] currentMetricPointBatch;
        private readonly AggregationType aggType;
        private readonly double[] histogramBounds;
        private readonly UpdateLongDelegate updateLongCallback;
        private readonly UpdateDoubleDelegate updateDoubleCallback;
        private readonly int maxMetricPoints;
        private int metricPointIndex = 0;
        private int batchSize = 0;
        private int metricCapHitMessageLogged;
        private bool zeroTagMetricPointInitialized;

        internal AggregatorStore(
            string name,
            AggregationType aggType,
            AggregationTemporality temporality,
            int maxMetricPoints,
            double[] histogramBounds,
            string[] tagKeysInteresting = null)
        {
            this.name = name;
            this.maxMetricPoints = maxMetricPoints;
            this.metricPointCapHitMessage = $"Maximum MetricPoints limit reached for this Metric stream. Configured limit: {this.maxMetricPoints}";
            this.metricPoints = new MetricPoint[maxMetricPoints];
            this.currentMetricPointBatch = new int[maxMetricPoints];
            this.aggType = aggType;
            this.outputDelta = temporality == AggregationTemporality.Delta;
            this.histogramBounds = histogramBounds;
            this.StartTimeExclusive = DateTimeOffset.UtcNow;
            if (tagKeysInteresting == null)
            {
                this.updateLongCallback = this.UpdateLong;
                this.updateDoubleCallback = this.UpdateDouble;
            }
            else
            {
                this.updateLongCallback = this.UpdateLongCustomTags;
                this.updateDoubleCallback = this.UpdateDoubleCustomTags;
                var hs = new HashSet<string>(tagKeysInteresting, StringComparer.Ordinal);
                this.tagKeysInteresting = hs;
                this.tagsKeysInterestingCount = hs.Count;
            }
        }

        private delegate void UpdateLongDelegate(long value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        private delegate void UpdateDoubleDelegate(double value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        internal DateTimeOffset StartTimeExclusive { get; private set; }

        internal DateTimeOffset EndTimeInclusive { get; private set; }

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
            var indexSnapshot = Math.Min(this.metricPointIndex, this.maxMetricPoints - 1);
            if (this.outputDelta)
            {
                this.SnapshotDelta(indexSnapshot);
            }
            else
            {
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

                metricPoint.TakeSnapshot(outputDelta: true);
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

                metricPoint.TakeSnapshot(outputDelta: false);
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
                        this.metricPoints[0] = new MetricPoint(this, this.aggType, null, null, this.histogramBounds);
                        this.zeroTagMetricPointInitialized = true;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LookupAggregatorStore(string[] tagKeys, object[] tagValues, int length)
        {
            var givenTags = new Tags(tagKeys, tagValues);

            if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out var aggregatorIndex))
            {
                if (length > 1)
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    // Create or obtain new arrays to temporarily hold the sorted tag Keys and Values
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CloneKeysAndValues(tagKeys, tagValues, length, out var tempSortedTagKeys, out var tempSortedTagValues);

                    Array.Sort(tempSortedTagKeys, tempSortedTagValues);

                    var sortedTags = new Tags(tempSortedTagKeys, tempSortedTagValues);

                    if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
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
                            var givenKeys = new string[length];
                            tagKeys.CopyTo(givenKeys, 0);

                            var givenValues = new object[length];
                            tagValues.CopyTo(givenValues, 0);

                            var sortedTagKeys = new string[length];
                            tempSortedTagKeys.CopyTo(sortedTagKeys, 0);

                            var sortedTagValues = new object[length];
                            tempSortedTagValues.CopyTo(sortedTagValues, 0);

                            givenTags = new Tags(givenKeys, givenValues);
                            sortedTags = new Tags(sortedTagKeys, sortedTagValues);
                        }

                        lock (this.tagsToMetricPointIndexDictionary)
                        {
                            // check again after acquiring lock.
                            if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
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
                                metricPoint = new MetricPoint(this, this.aggType, sortedTags.Keys, sortedTags.Values, this.histogramBounds);

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
                    if (aggregatorIndex >= this.maxMetricPoints)
                    {
                        // sorry! out of data points.
                        // TODO: Once we support cleanup of
                        // unused points (typically with delta)
                        // we can re-claim them here.
                        return -1;
                    }

                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                    var givenKeys = new string[length];
                    var givenValues = new object[length];

                    tagKeys.CopyTo(givenKeys, 0);
                    tagValues.CopyTo(givenValues, 0);

                    givenTags = new Tags(givenKeys, givenValues);

                    lock (this.tagsToMetricPointIndexDictionary)
                    {
                        // check again after acquiring lock.
                        if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out aggregatorIndex))
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
                            metricPoint = new MetricPoint(this, this.aggType, givenTags.Keys, givenTags.Values, this.histogramBounds);

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

                    return;
                }

                this.metricPoints[index].Update(value);
            }
            catch (Exception)
            {
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

                    return;
                }

                this.metricPoints[index].Update(value);
            }
            catch (Exception)
            {
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

                    return;
                }

                this.metricPoints[index].Update(value);
            }
            catch (Exception)
            {
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

                    return;
                }

                this.metricPoints[index].Update(value);
            }
            catch (Exception)
            {
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

            storage.SplitToKeysAndValues(tags, tagLength, out var tagKeys, out var tagValues);

            return this.LookupAggregatorStore(tagKeys, tagValues, tagLength);
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

            storage.SplitToKeysAndValues(tags, tagLength, this.tagKeysInteresting, out var tagKeys, out var tagValues, out var actualLength);

            // Actual number of tags depend on how many
            // of the incoming tags has user opted to
            // select.
            if (actualLength == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                return 0;
            }

            return this.LookupAggregatorStore(tagKeys, tagValues, actualLength);
        }
    }
}
