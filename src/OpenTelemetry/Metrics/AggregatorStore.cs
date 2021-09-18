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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    internal class AggregatorStore
    {
        internal const int MaxMetricPoints = 2000;
        private static readonly ObjectArrayEqualityComparer ObjectArrayComparer = new ObjectArrayEqualityComparer();
        private readonly object lockZeroTags = new object();

        // Two-Level lookup. TagKeys x [ TagValues x Metrics ]
        private readonly ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>> keyValue2MetricAggs =
            new ConcurrentDictionary<string[], ConcurrentDictionary<object[], int>>(new StringArrayEqualityComparer());

        private AggregationTemporality temporality;
        private MetricPoint[] metrics;
        private int metricPointIndex = 0;
        private bool zeroTagMetricPointInitialized;
        private AggregationType aggType;
        private DateTimeOffset startTimeExclusive;
        private DateTimeOffset endTimeInclusive;

        internal AggregatorStore(AggregationType aggType, AggregationTemporality temporality)
        {
            this.metrics = new MetricPoint[MaxMetricPoints];
            this.aggType = aggType;
            this.temporality = temporality;
            this.startTimeExclusive = DateTimeOffset.UtcNow;
        }

        internal int FindMetricAggregators(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            int len = tags.Length;
            if (len == 0)
            {
                if (!this.zeroTagMetricPointInitialized)
                {
                    lock (this.lockZeroTags)
                    {
                        if (!this.zeroTagMetricPointInitialized)
                        {
                            var dt = DateTimeOffset.UtcNow;
                            this.metrics[0] = new MetricPoint(this.aggType, dt, null, null);
                            this.zeroTagMetricPointInitialized = true;
                        }
                    }
                }

                return 0;
            }

            var storage = ThreadStaticStorage.GetStorage();

            storage.SplitToKeysAndValues(tags, out var tagKey, out var tagValue);

            if (len > 1)
            {
                Array.Sort<string, object>(tagKey, tagValue);
            }

            int aggregatorIndex;

            string[] seqKey = null;

            // GetOrAdd by TagKey at 1st Level of 2-level dictionary structure.
            // Get back a Dictionary of [ Values x Metrics[] ].
            if (!this.keyValue2MetricAggs.TryGetValue(tagKey, out var value2metrics))
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.
                seqKey = new string[len];
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
                            seqKey = new string[len];
                            tagKey.CopyTo(seqKey, 0);
                        }

                        var seqVal = new object[len];
                        tagValue.CopyTo(seqVal, 0);

                        ref var metricPoint = ref this.metrics[aggregatorIndex];
                        var dt = DateTimeOffset.UtcNow;
                        metricPoint = new MetricPoint(this.aggType, dt, seqKey, seqVal);

                        // Add to dictionary *after* initializing MetricPoint
                        // as other threads can start writing to the
                        // MetricPoint, if dictionary entry found.
                        value2metrics.TryAdd(seqVal, aggregatorIndex);
                    }
                }
            }

            return aggregatorIndex;
        }

        internal void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregators(tags);
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

        internal void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            try
            {
                var index = this.FindMetricAggregators(tags);
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

                metricPoint.TakeSnapShot(this.temporality == AggregationTemporality.Delta ? true : false);
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
    }
}
