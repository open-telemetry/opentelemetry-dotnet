// <copyright file="Histogram.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Histogram
{
    public abstract class Histogram<T>
        where T : IComparable<T>
    {
        protected readonly int NumberOfFiniteBuckets;
        protected readonly ConcurrentStack<T> Values = new ConcurrentStack<T>();

        private readonly long[] counts;
        private long[] overflowBucket = new long[1];
        private long[] underflowBucket = new long[1];

        protected Histogram(int numberOfFiniteBuckets)
        {
            if (numberOfFiniteBuckets < 0 || numberOfFiniteBuckets > 200)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numberOfFiniteBuckets), "The number of finite buckets must be between 0 and 200.");
            }

            this.NumberOfFiniteBuckets = numberOfFiniteBuckets;
            this.counts = new long[numberOfFiniteBuckets];
        }

        /// <summary>
        /// Atomically gets the histogram bucket counts (including overflow and underflow) and clears the buckets.
        /// </summary>
        /// <returns><see cref="DistributionData"/> representing the data collected in the histogram.</returns>
        public DistributionData GetDistributionAndClear()
        {
            lock (this.counts)
            lock (this.overflowBucket)
            lock (this.underflowBucket)
            lock (this.Values)
            {
                var distribution = this.Values.Count > 0
                    ? this.GetDistributionData()
                    : new DistributionData
                        {
                            BucketCounts = this.GetBucketCounts(),
                            Count = this.Values.Count,
                        };
                this.overflowBucket = new long[1];
                this.underflowBucket = new long[1];
                Array.Clear(this.counts, 0, this.NumberOfFiniteBuckets);
                this.Values.Clear();

                return distribution;
            }
        }

        public void RecordValue(T value)
        {
            this.Values.Push(value);
            this.UpdateBucketCounts(value);
        }

        protected abstract int GetBucketIndex(T valueToAdd);

        protected abstract DistributionData GetDistributionData();

        protected abstract T GetLowestBound();

        protected abstract T GetHighestBound();

        protected long[] GetBucketCounts()
        {
            if (this.NumberOfFiniteBuckets == 0)
            {
                return this.underflowBucket.Concat(this.overflowBucket).ToArray();
            }

            return this.underflowBucket.Concat(this.counts).Concat(this.overflowBucket).ToArray();
        }

        private void UpdateBucketCounts(T value)
        {
            // first check if value falls in overflow or underflow buckets
            if (value.CompareTo(this.GetLowestBound()) < 0)
            {
                Interlocked.Increment(ref this.underflowBucket[0]);
                return;
            }

            if (value.CompareTo(this.GetHighestBound()) >= 0)
            {
                Interlocked.Increment(ref this.overflowBucket[0]);
                return;
            }

            Interlocked.Increment(ref this.counts[this.GetBucketIndex(value)]);
        }
    }
}
