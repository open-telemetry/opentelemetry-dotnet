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

namespace OpenTelemetry.Metrics
{
    internal sealed class AggregatorStore
    {
        internal const int MaxMetricPoints = 2000;
        private static readonly ObjectArrayEqualityComparer ObjectArrayComparer = new ObjectArrayEqualityComparer();
        private static readonly StringArrayEqualityComparer StringArrayComparer = new StringArrayEqualityComparer();
        private readonly object lockZeroTags = new object();
        private readonly HashSet<string> tagKeysInteresting;
        private readonly int tagsKeysInterestingCount;

        // Two-Level lookup. TagKeys x [ TagValues x Metrics ]
        private readonly ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>> keyValue2MetricAggs =
            new ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>>(StringArrayComparer);

        private readonly AggregationTemporality temporality;
        private readonly bool outputDelta;
        private readonly MetricPoint[] metricPoints;
        private readonly int[] currentMetricPointBatch;
        private readonly AggregationType aggType;
        private readonly double[] histogramBounds;
        private readonly UpdateLongDelegate updateLongCallback;
        private readonly UpdateDoubleDelegate updateDoubleCallback;
        private int metricPointIndex = 0;
        private long batchSize = 0;
        private bool zeroTagMetricPointInitialized;
        private DateTimeOffset startTimeExclusive;
        private DateTimeOffset endTimeInclusive;

        internal AggregatorStore(
            AggregationType aggType,
            AggregationTemporality temporality,
            double[] histogramBounds,
            string[] tagKeysInteresting = null)
        {
            this.metricPoints = new MetricPoint[MaxMetricPoints];
            this.currentMetricPointBatch = new int[MaxMetricPoints];
            this.aggType = aggType;
            this.temporality = temporality;
            this.outputDelta = temporality == AggregationTemporality.Delta ? true : false;
            this.histogramBounds = histogramBounds;
            this.startTimeExclusive = DateTimeOffset.UtcNow;
            if (tagKeysInteresting == null)
            {
                this.updateLongCallback = this.UpdateLong;
                this.updateDoubleCallback = this.UpdateDouble;
            }
            else
            {
                this.updateLongCallback = this.UpdateLongCustomTags;
                this.updateDoubleCallback = this.UpdateDoubleCustomTags;
                var hs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in tagKeysInteresting)
                {
                    hs.Add(key);
                }

                this.tagKeysInteresting = hs;
                this.tagsKeysInterestingCount = hs.Count;
            }
        }

        private delegate void UpdateLongDelegate(long value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        private delegate void UpdateDoubleDelegate(double value, ReadOnlySpan<KeyValuePair<string, object>> tags);

        internal void Update(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.updateLongCallback(value, tags);
        }

        internal void Update(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.updateDoubleCallback(value, tags);
        }

        internal void SnapShot()
        {
            this.batchSize = 0;
            for (int i = 0; i < MaxMetricPoints; i++)
            {
                ref var metricPoint = ref this.metricPoints[i];

                // TODO: Consider support for marking cumulative temporality metrics stale
                if (this.temporality == AggregationTemporality.Cumulative)
                {
                    if (metricPoint.MetricPointStatus == MetricPointStatus.Unused
                        || metricPoint.MetricPointStatus == MetricPointStatus.UpdatePending)
                    {
                        continue;
                    }

                    metricPoint.TakeSnapShot(this.outputDelta);
                    this.currentMetricPointBatch[this.batchSize] = i;
                    this.batchSize++;
                }
                else
                {
                    switch (metricPoint.MetricPointStatus)
                    {
                        case MetricPointStatus.CollectPending:
                            metricPoint.TakeSnapShot(this.outputDelta);
                            this.currentMetricPointBatch[this.batchSize] = i;
                            this.batchSize++;
                            break;
                        case MetricPointStatus.NoCollectPending:
                            metricPoint.MarkStale();
                            break;
                        case MetricPointStatus.CandidateForRemoval:
                        case MetricPointStatus.Unused:
                        case MetricPointStatus.UpdatePending:
                        default:
                            break;
                    }
                }
            }

            if (this.temporality == AggregationTemporality.Delta)
            {
                if (this.endTimeInclusive != default)
                {
                    this.startTimeExclusive = this.endTimeInclusive;
                }
            }

            DateTimeOffset dt = DateTimeOffset.UtcNow;
            this.endTimeInclusive = dt;
        }

        internal BatchMetricPoint GetMetricPoints()
        {
            return new BatchMetricPoint(this.metricPoints, this.currentMetricPointBatch, this.batchSize, this.startTimeExclusive, this.endTimeInclusive);
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
                        var dt = DateTimeOffset.UtcNow;
                        this.metricPoints[0] = new MetricPoint(this.aggType, MetricPointStatus.UpdatePending, dt, null, null, this.histogramBounds);
                        this.zeroTagMetricPointInitialized = true;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LookupAggregatorStore(string[] tagKeys, object[] tagValues, int length)
        {
            int aggregatorIndex;
            string[] seqKey = null;

            // GetOrAdd by TagKeys at 1st Level of 2-level dictionary structure.
            // Get back a Dictionary of [ Values x Metrics[] ].
            if (!this.keyValue2MetricAggs.TryGetValue(tagKeys, out var value2metrics))
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                seqKey = new string[length];
                tagKeys.CopyTo(seqKey, 0);

                value2metrics = new ConcurrentDictionary<object[], int>(ObjectArrayComparer);
                if (!this.keyValue2MetricAggs.TryAdd(seqKey, value2metrics))
                {
                    this.keyValue2MetricAggs.TryGetValue(seqKey, out value2metrics);
                }
            }

            // GetOrAdd by TagValues at 2st Level of 2-level dictionary structure.
            // Get back Metrics[].
            if (!value2metrics.TryGetValue(tagValues, out aggregatorIndex))
            {
                lock (value2metrics)
                {
                    // check again after acquiring lock.
                    if (!value2metrics.TryGetValue(tagValues, out aggregatorIndex))
                    {
                        if (!this.TryGetUnusedMetricPoint(out aggregatorIndex))
                        {
                            // sorry! out of data points.
                            return -1;
                        }

                        // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                        if (seqKey == null)
                        {
                            seqKey = new string[length];
                            tagKeys.CopyTo(seqKey, 0);
                        }

                        var seqVal = new object[length];
                        tagValues.CopyTo(seqVal, 0);

                        ref var metricPoint = ref this.metricPoints[aggregatorIndex];
                        var dt = DateTimeOffset.UtcNow;
                        metricPoint = new MetricPoint(this.aggType, MetricPointStatus.UpdatePending, dt, seqKey, seqVal, this.histogramBounds);

                        // Add to dictionary *after* initializing MetricPoint
                        // as other threads can start writing to the
                        // MetricPoint, if dictionary entry found.
                        value2metrics.TryAdd(seqVal, aggregatorIndex);
                    }
                }
            }

            return aggregatorIndex;
        }

        private bool TryGetUnusedMetricPoint(out int index)
        {
            if (this.metricPointIndex < MaxMetricPoints)
            {
                index = Interlocked.Increment(ref this.metricPointIndex);
                if (index < MaxMetricPoints)
                {
                    return true;
                }
            }

            if (this.temporality == AggregationTemporality.Delta)
            {
                for (int i = 1; i < MaxMetricPoints; i++)
                {
                    ref var metricPoint = ref this.metricPoints[i];
                    if (metricPoint.MetricPointStatus == MetricPointStatus.CandidateForRemoval)
                    {
                        if (metricPoint.TryFree())
                        {
                            var t = this.keyValue2MetricAggs[metricPoint.Keys];
                            t.TryRemove(metricPoint.Values, out var _);

                            index = i;
                            return true;
                        }
                    }
                }
            }

            index = -1;
            return false;
        }

        private void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                int index;
                string[] tagKeys;
                object[] tagValues;
                do
                {
                    index = this.FindMetricAggregatorsDefault(tags, out tagKeys, out tagValues);
                    if (index < 0)
                    {
                        // TODO: Measurement dropped due to MemoryPoint cap hit.
                        return;
                    }
                }
                while (!this.TryUpdateMetricPoint(index, value, tagKeys, tagValues));
            }
            catch (Exception)
            {
                // TODO: Measurement dropped due to internal exception.
            }
        }

        private void UpdateLongCustomTags(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                int index;
                string[] tagKeys;
                object[] tagValues;
                do
                {
                    index = this.FindMetricAggregatorsCustomTag(tags, out tagKeys, out tagValues);
                    if (index < 0)
                    {
                        // TODO: Measurement dropped due to MemoryPoint cap hit.
                        return;
                    }
                }
                while (!this.TryUpdateMetricPoint(index, value, tagKeys, tagValues));
            }
            catch (Exception)
            {
                // TODO: Measurement dropped due to internal exception.
            }
        }

        private void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                int index;
                string[] tagKeys;
                object[] tagValues;
                do
                {
                    index = this.FindMetricAggregatorsDefault(tags, out tagKeys, out tagValues);
                    if (index < 0)
                    {
                        // TODO: Measurement dropped due to MemoryPoint cap hit.
                        return;
                    }
                }
                while (!this.TryUpdateMetricPoint(index, value, tagKeys, tagValues));
            }
            catch (Exception)
            {
                // TODO: Measurement dropped due to internal exception.
            }
        }

        private void UpdateDoubleCustomTags(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                int index;
                string[] tagKeys;
                object[] tagValues;
                do
                {
                    index = this.FindMetricAggregatorsCustomTag(tags, out tagKeys, out tagValues);
                    if (index < 0)
                    {
                        // TODO: Measurement dropped due to MemoryPoint cap hit.
                        return;
                    }
                }
                while (!this.TryUpdateMetricPoint(index, value, tagKeys, tagValues));
            }
            catch (Exception)
            {
                // TODO: Measurement dropped due to internal exception.
            }
        }

        private bool TryUpdateMetricPoint(int index, long value, string[] tagKeys, object[] tagValues)
        {
            ref var metricPoint = ref this.metricPoints[index];

            // If the MetricPointStatus = CandidateForRemoval, it is possible that another thread freed and repurposed the MetricPoint.
            // We must check that the point being updated is the expected one.
            if (StringArrayComparer.Equals(metricPoint.Keys, tagKeys) && ObjectArrayComparer.Equals(metricPoint.Values, tagValues))
            {
                return metricPoint.Update(value);
            }
            else
            {
                return false;
            }
        }

        private bool TryUpdateMetricPoint(int index, double value, string[] tagKeys, object[] tagValues)
        {
            ref var metricPoint = ref this.metricPoints[index];

            // If the MetricPointStatus = CandidateForRemoval, it is possible that another thread freed and repurposed the MetricPoint.
            // We must check that the point being updated is the expected one.
            if (metricPoint.Keys == tagKeys && metricPoint.Values == tagValues)
            {
                return metricPoint.Update(value);
            }
            else
            {
                return false;
            }
        }

        private int FindMetricAggregatorsDefault(ReadOnlySpan<KeyValuePair<string, object>> tags, out string[] tagKeys, out object[] tagValues)
        {
            int tagLength = tags.Length;
            if (tagLength == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                tagKeys = null;
                tagValues = null;
                return 0;
            }

            var storage = ThreadStaticStorage.GetStorage();

            storage.SplitToKeysAndValues(tags, tagLength, out tagKeys, out tagValues);

            if (tagLength > 1)
            {
                Array.Sort(tagKeys, tagValues);
            }

            return this.LookupAggregatorStore(tagKeys, tagValues, tagLength);
        }

        private int FindMetricAggregatorsCustomTag(ReadOnlySpan<KeyValuePair<string, object>> tags, out string[] tagKeys, out object[] tagValues)
        {
            int tagLength = tags.Length;
            if (tagLength == 0 || this.tagsKeysInterestingCount == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                tagKeys = null;
                tagValues = null;
                return 0;
            }

            // TODO: Get only interesting tags
            // from the incoming tags

            var storage = ThreadStaticStorage.GetStorage();

            storage.SplitToKeysAndValues(tags, tagLength, this.tagKeysInteresting, out tagKeys, out tagValues, out var actualLength);

            // Actual number of tags depend on how many
            // of the incoming tags has user opted to
            // select.
            if (actualLength == 0)
            {
                this.InitializeZeroTagPointIfNotInitialized();
                return 0;
            }

            if (actualLength > 1)
            {
                Array.Sort(tagKeys, tagValues);
            }

            return this.LookupAggregatorStore(tagKeys, tagValues, actualLength);
        }
    }
}
