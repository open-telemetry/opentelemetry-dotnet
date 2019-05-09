// <copyright file="Distribution.cs" company="OpenCensus Authors">
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

    public sealed class Distribution : Aggregation, IDistribution
    {
        private Distribution()
        {
        }

        private Distribution(IBucketBoundaries bucketBoundaries)
        {
            this.BucketBoundaries = bucketBoundaries ?? throw new ArgumentNullException("Null bucketBoundaries");
        }

        public IBucketBoundaries BucketBoundaries { get; }

        public static IDistribution Create(IBucketBoundaries bucketBoundaries)
        {
            if (bucketBoundaries == null)
            {
                throw new ArgumentNullException(nameof(bucketBoundaries));
            }

            return new Distribution(bucketBoundaries);
        }

        public override T Match<T>(Func<ISum, T> p0, Func<ICount, T> p1, Func<IMean, T> p2, Func<IDistribution, T> p3, Func<ILastValue, T> p4, Func<IAggregation, T> p5)
        {
            return p3.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Distribution{"
                + "bucketBoundaries=" + this.BucketBoundaries
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Distribution that)
            {
                return this.BucketBoundaries.Equals(that.BucketBoundaries);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.BucketBoundaries.GetHashCode();
            return h;
        }
    }
}
