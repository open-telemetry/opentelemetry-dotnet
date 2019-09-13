// <copyright file="DistributionData.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Stats.Aggregations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Utils;

    [DebuggerDisplay("{ToString(),nq}")]
    public class DistributionData : AggregationData, IDistributionData
    {
        internal DistributionData(double mean, long count, double min, double max, double sumOfSquaredDeviations, IReadOnlyList<long> bucketCounts)
        {
            this.Mean = mean;
            this.Count = count;
            this.Min = min;
            this.Max = max;
            this.SumOfSquaredDeviations = sumOfSquaredDeviations;
            this.BucketCounts = bucketCounts ?? throw new ArgumentNullException(nameof(bucketCounts));
        }

        public double Mean { get; }

        public long Count { get; }

        public double Min { get; }

        public double Max { get; }

        public double SumOfSquaredDeviations { get; }

        public IReadOnlyList<long> BucketCounts { get; }

        public static IDistributionData Create(double mean, long count, double min, double max, double sumOfSquaredDeviations, IReadOnlyList<long> bucketCounts)
        {
            if (!double.IsPositiveInfinity(min) || !double.IsNegativeInfinity(max))
            {
                if (!(min <= max))
                {
                    throw new ArgumentOutOfRangeException(nameof(max), "max should be greater or equal to min.");
                }
            }

            if (bucketCounts == null)
            {
                throw new ArgumentNullException(nameof(bucketCounts));
            }

            IReadOnlyList<long> bucketCountsCopy = new List<long>(bucketCounts).AsReadOnly();

            return new DistributionData(
                mean, count, min, max, sumOfSquaredDeviations, bucketCountsCopy);
        }

        public override T Match<T>(
            Func<ISumDataDouble, T> p0,
            Func<ISumDataLong, T> p1,
            Func<ICountData, T> p2,
            Func<IMeanData, T> p3,
            Func<IDistributionData, T> p4,
            Func<ILastValueDataDouble, T> p5,
            Func<ILastValueDataLong, T> p6,
            Func<IAggregationData, T> defaultFunction)
        {
            return p4.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(DistributionData)
                + "{"
                + nameof(this.Mean) + "=" + this.Mean + ", "
                + nameof(this.Count) + "=" + this.Count + ", "
                + nameof(this.Min) + "=" + this.Min + ", "
                + nameof(this.Max) + "=" + this.Max + ", "
                + nameof(this.SumOfSquaredDeviations) + "=" + this.SumOfSquaredDeviations + ", "
                + nameof(this.BucketCounts) + "=" + string.Join(", ", this.BucketCounts)
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is DistributionData that)
            {
                return (DoubleUtil.ToInt64(this.Mean) == DoubleUtil.ToInt64(that.Mean))
                     && (this.Count == that.Count)
                     && (DoubleUtil.ToInt64(this.Min) == DoubleUtil.ToInt64(that.Min))
                     && (DoubleUtil.ToInt64(this.Max) == DoubleUtil.ToInt64(that.Max))
                     && (DoubleUtil.ToInt64(this.SumOfSquaredDeviations) == DoubleUtil.ToInt64(that.SumOfSquaredDeviations))
                     && this.BucketCounts.SequenceEqual(that.BucketCounts);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.Mean) >> 32) ^ DoubleUtil.ToInt64(this.Mean);
            h *= 1000003;
            h ^= (this.Count >> 32) ^ this.Count;
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.Min) >> 32) ^ DoubleUtil.ToInt64(this.Min);
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.Max) >> 32) ^ DoubleUtil.ToInt64(this.Max);
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.SumOfSquaredDeviations) >> 32) ^ DoubleUtil.ToInt64(this.SumOfSquaredDeviations);
            h *= 1000003;
            h ^= this.BucketCounts.GetHashCode();
            return (int)h;
        }
    }
}
