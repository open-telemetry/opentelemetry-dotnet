// <copyright file="MeanData.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Stats.Aggregations
{
    using System;
    using OpenCensus.Utils;

    public class MeanData : AggregationData, IMeanData
    {
        internal MeanData(double mean, long count, double min, double max)
        {
            this.Mean = mean;
            this.Count = count;
            this.Min = min;
            this.Max = max;
        }

        public double Mean { get; }

        public long Count { get; }

        public double Max { get; }

        public double Min { get; }

        public static IMeanData Create(double mean, long count, double min, double max)
        {
            return new MeanData(mean, count, min, max);
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
            return p3.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "MeanData{"
                + "mean=" + this.Mean + ", "
                + "min=" + this.Min + ", "
                + "max=" + this.Max + ", "
                + "count=" + this.Count
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is MeanData that)
            {
                return (DoubleUtil.ToInt64(this.Mean) == DoubleUtil.ToInt64(that.Mean))
                        && (this.Count == that.Count)
                        && (DoubleUtil.ToInt64(this.Min) == DoubleUtil.ToInt64(that.Min))
                        && (DoubleUtil.ToInt64(this.Max) == DoubleUtil.ToInt64(that.Max));
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
            h ^= (DoubleUtil.ToInt64(this.Min) >> 32) ^ DoubleUtil.ToInt64(this.Min);
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.Max) >> 32) ^ DoubleUtil.ToInt64(this.Max);
            h *= 1000003;
            h ^= (this.Count >> 32) ^ this.Count;
            return (int)h;
        }
    }
}
