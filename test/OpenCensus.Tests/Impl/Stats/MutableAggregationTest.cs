// <copyright file="MutableAggregationTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class MutableAggregationTest
    {
        private const double TOLERANCE = 1e-6;
        private static readonly IBucketBoundaries BUCKET_BOUNDARIES = BucketBoundaries.Create(new List<double>() { -10.0, 0.0, 10.0 });

        [Fact]
        public void TestCreateEmpty()
        {
            Assert.InRange(MutableSum.Create().Sum, 0 - TOLERANCE, 0 + TOLERANCE);
            Assert.Equal(0, MutableCount.Create().Count);
            Assert.InRange(MutableMean.Create().Mean, 0 - TOLERANCE, 0 + TOLERANCE);
            Assert.True(Double.IsNaN(MutableLastValue.Create().LastValue));

            IBucketBoundaries bucketBoundaries = BucketBoundaries.Create(new List<double>() { 0.1, 2.2, 33.3 });
            MutableDistribution mutableDistribution = MutableDistribution.Create(bucketBoundaries);
            Assert.InRange(mutableDistribution.Mean, 0, TOLERANCE);
            Assert.Equal(0, mutableDistribution.Count);
            Assert.Equal(double.PositiveInfinity, mutableDistribution.Min);
            Assert.Equal(double.NegativeInfinity, mutableDistribution.Max);
            Assert.InRange(mutableDistribution.SumOfSquaredDeviations, 0 - TOLERANCE, 0 + TOLERANCE);
            Assert.Equal(new long[4], mutableDistribution.BucketCounts);
        }

        [Fact]
        public void TestNullBucketBoundaries()
        {

            Assert.Throws<ArgumentNullException>(() => MutableDistribution.Create(null));
        }

        [Fact]
        public void TestNoBoundaries()
        {
            List<Double> buckets = new List<double>();
            MutableDistribution noBoundaries = MutableDistribution.Create(BucketBoundaries.Create(buckets));
            Assert.Single(noBoundaries.BucketCounts);
            Assert.Equal(0, noBoundaries.BucketCounts[0]);
        }

        [Fact]
        public void TestAdd()
        {
            List<MutableAggregation> aggregations =
                new List<MutableAggregation>(){
                MutableSum.Create(),
                MutableCount.Create(),
                MutableMean.Create(),
                MutableDistribution.Create(BUCKET_BOUNDARIES),
                MutableLastValue.Create(),};

            List<double> values = new List<double>() { -1.0, 1.0, -5.0, 20.0, 5.0 };

            foreach (double value in values)
            {
                foreach (MutableAggregation aggregation in aggregations)
                {
                    aggregation.Add(value);
                }
            }

            foreach (MutableAggregation aggregation in aggregations)
            {
                aggregation.Match<object>(
                    (arg) =>
                    {
                        Assert.InRange(arg.Sum, 20.0 - TOLERANCE, 20.0 + TOLERANCE);
                        return null;
                    },
                    (arg) =>

                    {
                        Assert.Equal(5, arg.Count);
                        return null;

                    },
                    (arg) =>

                    {
                        Assert.InRange(arg.Mean, 4.0 - TOLERANCE, 4.0 + TOLERANCE);
                        Assert.InRange(arg.Max, 20.0 - TOLERANCE, 20 + TOLERANCE);
                        Assert.InRange(arg.Min, -5.0 - TOLERANCE, -5.0 + TOLERANCE);
                        return null;

                    },
                    (arg) =>
                    {
                        Assert.Equal(new long[] { 0, 2, 2, 1 }, arg.BucketCounts);
                        return null;
                    },
                    (arg) =>
                    {
                        Assert.InRange(arg.LastValue, 5.0 - TOLERANCE, 5.0 + TOLERANCE);
                        return null;
                    }
                    );
            }
        }

        [Fact]
        public void TestMatch()
        {
            List<MutableAggregation> aggregations =
                new List<MutableAggregation>(){
                MutableSum.Create(),
                MutableCount.Create(),
                MutableMean.Create(),
                MutableDistribution.Create(BUCKET_BOUNDARIES),
                MutableLastValue.Create(),};

            List<String> actual = new List<String>();
            foreach (MutableAggregation aggregation in aggregations)
            {
                actual.Add(
                    aggregation.Match(
                        (arg) =>
                        {
                            return "SUM";

                        },
                        (arg) =>

                        {
                            return "COUNT";
                        },
                        (arg) =>
                        {
                            return "MEAN";
                        },
                        (arg) =>
                        {
                            return "DISTRIBUTION";
                        },
                        (arg) =>
                        {
                            return "LASTVALUE";
                        }
                        )
                        );
            }

            Assert.Equal(new List<string>() { "SUM", "COUNT", "MEAN", "DISTRIBUTION", "LASTVALUE" }, actual);
        }

        [Fact]
        public void TestCombine_SumCountMean()
        {
            // combine() for Mutable Sum, Count and Mean will pick up fractional stats
            List<MutableAggregation> aggregations1 =
                new List<MutableAggregation>() { MutableSum.Create(), MutableCount.Create(), MutableMean.Create() };
            List<MutableAggregation> aggregations2 =
                new List<MutableAggregation>() { MutableSum.Create(), MutableCount.Create(), MutableMean.Create() };

            foreach (double val in new List<double>() { -1.0, -5.0 })
            {
                foreach (MutableAggregation aggregation in aggregations1)
                {
                    aggregation.Add(val);
                }
            }
            foreach (double val in new List<double>() { 10.0, 50.0 })
            {
                foreach (MutableAggregation aggregation in aggregations2)
                {
                    aggregation.Add(val);
                }
            }

            List<MutableAggregation> combined =
                new List<MutableAggregation>() { MutableSum.Create(), MutableCount.Create(), MutableMean.Create() };
            double fraction1 = 1.0;
            double fraction2 = 0.6;
            for (int i = 0; i < combined.Count; i++)
            {
                combined[i].Combine(aggregations1[i], fraction1);
                combined[i].Combine(aggregations2[i], fraction2);
            }

            Assert.InRange(((MutableSum)combined[0]).Sum, 30 - TOLERANCE, 30 + TOLERANCE);
            Assert.Equal(3, ((MutableCount)combined[1]).Count);
            Assert.InRange(((MutableMean)combined[2]).Mean, 10 - TOLERANCE, 10 + TOLERANCE);
        }

        [Fact]
        public void TestCombine_Distribution()
        {
            // combine() for Mutable Distribution will ignore fractional stats
            MutableDistribution distribution1 = MutableDistribution.Create(BUCKET_BOUNDARIES);
            MutableDistribution distribution2 = MutableDistribution.Create(BUCKET_BOUNDARIES);
            MutableDistribution distribution3 = MutableDistribution.Create(BUCKET_BOUNDARIES);

            foreach (double val in new List<double>() { 5.0, -5.0 })
            {
                distribution1.Add(val);
            }
            foreach (double val in new List<double>() { 10.0, 20.0 })
            {
                distribution2.Add(val);
            }
            foreach (double val in new List<double>() { -10.0, 15.0, -15.0, -20.0 })
            {
                distribution3.Add(val);
            }

            MutableDistribution combined = MutableDistribution.Create(BUCKET_BOUNDARIES);
            combined.Combine(distribution1, 1.0); // distribution1 will be combined
            combined.Combine(distribution2, 0.6); // distribution2 will be ignored
            VerifyMutableDistribution(combined, 0, 2, -5, 5, 50.0, new long[] { 0, 1, 1, 0 }, TOLERANCE);

            combined.Combine(distribution2, 1.0); // distribution2 will be combined
            VerifyMutableDistribution(combined, 7.5, 4, -5, 20, 325.0, new long[] { 0, 1, 1, 2 }, TOLERANCE);

            combined.Combine(distribution3, 1.0); // distribution3 will be combined
            VerifyMutableDistribution(combined, 0, 8, -20, 20, 1500.0, new long[] { 2, 2, 1, 3 }, TOLERANCE);
        }

        private static void VerifyMutableDistribution(
            MutableDistribution mutableDistribution,
            double mean,
            long count,
            double min,
            double max,
            double sumOfSquaredDeviations,
            long[] bucketCounts,
            double tolerance)
        {
            Assert.InRange(mutableDistribution.Mean, mean - tolerance, mean + tolerance);
            Assert.Equal(count, mutableDistribution.Count);
            Assert.InRange(mutableDistribution.Min, min - tolerance, min + tolerance);
            Assert.InRange(mutableDistribution.Max, max - tolerance, max + tolerance);
            Assert.InRange(mutableDistribution.SumOfSquaredDeviations, sumOfSquaredDeviations - tolerance, sumOfSquaredDeviations + tolerance);
            Assert.Equal(bucketCounts, mutableDistribution.BucketCounts);
        }
    }
}
