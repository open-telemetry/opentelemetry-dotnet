// <copyright file="LastValueDataLong.cs" company="OpenCensus Authors">
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

    public sealed class LastValueDataLong : AggregationData, ILastValueDataLong
    {
        private LastValueDataLong()
        {
        }

        private LastValueDataLong(long lastValue)
        {
            this.LastValue = lastValue;
        }

        public long LastValue { get; }

        public static ILastValueDataLong Create(long lastValue)
        {
            return new LastValueDataLong(lastValue);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "LastValueDataLong{"
                + "lastValue=" + this.LastValue
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is LastValueDataLong that)
            {
                return this.LastValue == that.LastValue;
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= (this.LastValue >> 32) ^ this.LastValue;
            return (int)h;
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
            return p6.Invoke(this);
        }
    }
}
