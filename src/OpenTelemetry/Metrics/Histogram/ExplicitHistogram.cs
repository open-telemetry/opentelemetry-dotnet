// <copyright file="ExplicitHistogram.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Histogram
{
    public abstract class ExplicitHistogram<T> : Histogram<T>
        where T : IComparable<T>
    {
        protected ExplicitHistogram(T[] bounds)
            : base(bounds.Length - 1)
        {
            this.Bounds = new T[bounds.Length];
            Array.Copy(bounds, this.Bounds, bounds.Length);
        }

        public T[] Bounds { get; }

        protected override int GetBucketIndex(T valueToAdd)
        {
            var startingIndex = (int)Math.Ceiling((double)this.NumberOfFiniteBuckets / 2);
            return this.BinarySearchForBucketIndex(valueToAdd, startingIndex, 0, this.NumberOfFiniteBuckets - 1);
        }

        protected override T GetLowestBound()
        {
            return this.Bounds[0];
        }

        protected override T GetHighestBound()
        {
            return this.Bounds[this.Bounds.Length - 1];
        }

        /// <summary>
        /// Binary search for the appropriate bucket index where the value to add is greater than or equal to the
        /// index's bound and is less than the bound of the next index.
        ///
        /// This method assumes that the value is within [lowerBound, upperBound). If the value is outside this range,
        /// it should be part of the underflow or overflow buckets.
        /// </summary>
        private int BinarySearchForBucketIndex(T valueToAdd, int index, int minIndex, int maxIndex)
        {
            var compareTo = valueToAdd.CompareTo(this.Bounds[index]);

            if (compareTo == 0)
            {
                return index;
            }

            if (compareTo < 0)
            {
                var lowerIndex = index - (int)Math.Ceiling((double)(index - minIndex) / 2);
                return this.BinarySearchForBucketIndex(valueToAdd, lowerIndex, minIndex, index);
            }

            if (valueToAdd.CompareTo(this.Bounds[index + 1]) < 0)
            {
                return index;
            }

            var greaterIndex = index + (int)Math.Ceiling((double)(maxIndex - index) / 2);
            return this.BinarySearchForBucketIndex(valueToAdd, greaterIndex, index, maxIndex);
        }
    }
}
