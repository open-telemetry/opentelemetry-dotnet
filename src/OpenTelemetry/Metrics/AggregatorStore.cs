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
        private readonly object lockZeroTags = new object();
        private readonly HashSet<string> tagKeysInteresting;
        private readonly int tagsKeysInterestingCount;

        // Two-Level lookup. TagKeys x [ TagValues x Metrics ]
        private readonly ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>> keyValue2MetricAggs =
            new ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>>(new StringArrayEqualityComparer());

        private readonly AggregationTemporality temporality;
        private readonly bool outputDelta;
        private readonly MetricPoint[] metrics;
        private readonly AggregationType aggType;
        private readonly double[] histogramBounds;
        private readonly UpdateLongDelegate updateLongCallback;
        private readonly UpdateDoubleDelegate updateDoubleCallback;
        private int metricPointIndex = 0;
        private bool zeroTagMetricPointInitialized;
        private DateTimeOffset startTimeExclusive;
        private DateTimeOffset endTimeInclusive;

        internal AggregatorStore(
            AggregationType aggType,
            AggregationTemporality temporality,
            double[] histogramBounds,
            string[] tagKeysInteresting = null)
        {
            this.metrics = new MetricPoint[MaxMetricPoints];
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
            var indexSnapShot = Math.Min(this.metricPointIndex, MaxMetricPoints - 1);

            for (int i = 0; i <= indexSnapShot; i++)
            {
                ref var metricPoint = ref this.metrics[i];
                if (metricPoint.StartTime == default)
                {
                    continue;
                }

                metricPoint.TakeSnapShot(this.outputDelta);
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
            var indexSnapShot = Math.Min(this.metricPointIndex, MaxMetricPoints - 1);
            return new BatchMetricPoint(this.metrics, indexSnapShot + 1, this.startTimeExclusive, this.endTimeInclusive);
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
                        this.metrics[0] = new MetricPoint(this.aggType, dt, null, null, this.histogramBounds);
                        this.zeroTagMetricPointInitialized = true;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LookupAggregatorStore(string[] tagKey, object[] tagValue, int length)
        {
            int aggregatorIndex;
            string[] seqKey = null;

            // GetOrAdd by TagKey at 1st Level of 2-level dictionary structure.
            // Get back a Dictionary of [ Values x Metrics[] ].
            if (!this.keyValue2MetricAggs.TryGetValue(tagKey, out var value2metrics))
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                seqKey = new string[length];
                tagKey.CopyTo(seqKey, 0);

                value2metrics = new ConcurrentDictionary<object[], int>(ObjectArrayComparer);
                if (!this.keyValue2MetricAggs.TryAdd(seqKey, value2metrics))
                {
                    this.keyValue2MetricAggs.TryGetValue(seqKey, out value2metrics);
                }
            }

            // GetOrAdd by TagValue at 2st Level of 2-level dictionary structure.
            // Get back Metrics[].
            if (!value2metrics.TryGetValue(tagValue, out aggregatorIndex))
            {
                aggregatorIndex = this.metricPointIndex;
                if (aggregatorIndex >= MaxMetricPoints)
                {
                    // sorry! out of data points.
                    // TODO: Once we support cleanup of
                    // unused points (typically with delta)
                    // we can re-claim them here.
                    return -1;
                }

                lock (value2metrics)
                {
                    // check again after acquiring lock.
                    if (!value2metrics.TryGetValue(tagValue, out aggregatorIndex))
                    {
                        aggregatorIndex = Interlocked.Increment(ref this.metricPointIndex);
                        if (aggregatorIndex >= MaxMetricPoints)
                        {
                            // sorry! out of data points.
                            // TODO: Once we support cleanup of
                            // unused points (typically with delta)
                            // we can re-claim them here.
                            return -1;
                        }

                        // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                        if (seqKey == null)
                        {
                            seqKey = new string[length];
                            tagKey.CopyTo(seqKey, 0);
                        }

                        var seqVal = new object[length];
                        tagValue.CopyTo(seqVal, 0);

                        ref var metricPoint = ref this.metrics[aggregatorIndex];
                        var dt = DateTimeOffset.UtcNow;
                        metricPoint = new MetricPoint(this.aggType, dt, seqKey, seqVal, this.histogramBounds);

                        // Add to dictionary *after* initializing MetricPoint
                        // as other threads can start writing to the
                        // MetricPoint, if dictionary entry found.
                        value2metrics.TryAdd(seqVal, aggregatorIndex);
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
                    // TODO: Measurement dropped due to MemoryPoint cap hit.
                    return;
                }

                this.metrics[index].Update(value);
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
                var index = this.FindMetricAggregatorsCustomTag(tags);
                if (index < 0)
                {
                    // TODO: Measurement dropped due to MemoryPoint cap hit.
                    return;
                }

                this.metrics[index].Update(value);
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
                var index = this.FindMetricAggregatorsDefault(tags);
                if (index < 0)
                {
                    // TODO: Measurement dropped due to MemoryPoint cap hit.
                    return;
                }

                this.metrics[index].Update(value);
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
                var index = this.FindMetricAggregatorsCustomTag(tags);
                if (index < 0)
                {
                    // TODO: Measurement dropped due to MemoryPoint cap hit.
                    return;
                }

                this.metrics[index].Update(value);
            }
            catch (Exception)
            {
                // TODO: Measurement dropped due to internal exception.
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

            storage.SplitToKeysAndValues(tags, tagLength, out var tagKey, out var tagValue);

            if (tagLength > 1)
            {
                Array.Sort<string, object>(tagKey, tagValue);
            }

            return this.LookupAggregatorStore(tagKey, tagValue, tagLength);
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

            storage.SplitToKeysAndValues(tags, tagLength, this.tagKeysInteresting, out var tagKey, out var tagValue, out var actualLength);

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
                Array.Sort<string, object>(tagKey, tagValue);
            }

            return this.LookupAggregatorStore(tagKey, tagValue, actualLength);
        }
    }
}
