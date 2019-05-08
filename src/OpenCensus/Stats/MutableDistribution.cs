// <copyright file="MutableDistribution.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats
{
    using System;

    internal sealed class MutableDistribution : MutableAggregation
    {
        private const double TOLERANCE = 1e-6;

        internal MutableDistribution(IBucketBoundaries bucketBoundaries)
        {
            this.BucketBoundaries = bucketBoundaries;
            this.BucketCounts = new long[bucketBoundaries.Boundaries.Count + 1];
        }

        internal double Sum { get; set; } = 0.0;

        internal double Mean { get; set; } = 0.0;

        internal long Count { get; set; } = 0;

        internal double SumOfSquaredDeviations { get; set; } = 0.0;

        // Initial "impossible" values, that will get reset as soon as first value is added.
        internal double Min { get; set; } = double.PositiveInfinity;

        internal double Max { get; set; } = double.NegativeInfinity;

        internal IBucketBoundaries BucketBoundaries { get; }

        internal long[] BucketCounts { get; }

        internal static MutableDistribution Create(IBucketBoundaries bucketBoundaries)
        {
            if (bucketBoundaries == null)
            {
                throw new ArgumentNullException(nameof(bucketBoundaries));
            }

            return new MutableDistribution(bucketBoundaries);
        }

        internal override void Add(double value)
        {
            this.Sum += value;
            this.Count++;

            /*
             * Update the sum of squared deviations from the mean with the given value. For values
             * x_i this is Sum[i=1..n]((x_i - mean)^2)
             *
             * Computed using Welfords method (see
             * https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance, or Knuth, "The Art of
             * Computer Programming", Vol. 2, page 323, 3rd edition)
             */
            double deltaFromMean = value - this.Mean;
            this.Mean += deltaFromMean / this.Count;
            double deltaFromMean2 = value - this.Mean;
            this.SumOfSquaredDeviations += deltaFromMean * deltaFromMean2;

            if (value < this.Min)
            {
                this.Min = value;
            }

            if (value > this.Max)
            {
                this.Max = value;
            }

            for (int i = 0; i < this.BucketBoundaries.Boundaries.Count; i++)
            {
                if (value < this.BucketBoundaries.Boundaries[i])
                {
                    this.BucketCounts[i]++;
                    return;
                }
            }

            this.BucketCounts[this.BucketCounts.Length - 1]++;
        }

        // We don't compute fractional MutableDistribution, it's either whole or none.
        internal override void Combine(MutableAggregation other, double fraction)
        {
            if (!(other is MutableDistribution mutableDistribution))
            {
                throw new ArgumentException("MutableDistribution expected.");
            }

            if (Math.Abs(1.0 - fraction) > TOLERANCE)
            {
                return;
            }

            if (!this.BucketBoundaries.Equals(mutableDistribution.BucketBoundaries))
            {
                throw new ArgumentException("Bucket boundaries should match.");
            }

            // Algorithm for calculating the combination of sum of squared deviations:
            // https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Parallel_algorithm.
            if (this.Count + mutableDistribution.Count > 0)
            {
                double delta = mutableDistribution.Mean - this.Mean;
                this.SumOfSquaredDeviations =
                    this.SumOfSquaredDeviations
                        + mutableDistribution.SumOfSquaredDeviations
                        + (Math.Pow(delta, 2)
                            * this.Count
                            * mutableDistribution.Count
                            / (this.Count + mutableDistribution.Count));
            }

            this.Count += mutableDistribution.Count;
            this.Sum += mutableDistribution.Sum;
            this.Mean = this.Sum / this.Count;

            if (mutableDistribution.Min < this.Min)
            {
                this.Min = mutableDistribution.Min;
            }

            if (mutableDistribution.Max > this.Max)
            {
                this.Max = mutableDistribution.Max;
            }

            long[] bucketCounts = mutableDistribution.BucketCounts;
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                this.BucketCounts[i] += bucketCounts[i];
            }
        }

        internal override T Match<T>(Func<MutableSum, T> p0, Func<MutableCount, T> p1, Func<MutableMean, T> p2, Func<MutableDistribution, T> p3, Func<MutableLastValue, T> p4)
        {
            return p3.Invoke(this);
        }
    }
}
