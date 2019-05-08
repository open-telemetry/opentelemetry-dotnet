// <copyright file="AggregationDataTest.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Aggregations;
    using Xunit;

    public class AggregationDataTest
    {
        private static readonly double TOLERANCE = 1e-6;

        [Fact]
        public void TestCreateDistributionData()
        {
            IDistributionData distributionData =
                DistributionData.Create(7.7, 10, 1.1, 9.9, 32.2, new List<long>() { 4L, 1L, 5L });

            Assert.InRange(distributionData.Mean, 7.7 - TOLERANCE, 7.7 + TOLERANCE);
            Assert.Equal(10, distributionData.Count);
            Assert.InRange(distributionData.Min, 1.1 - TOLERANCE, 1.1 + TOLERANCE);
            Assert.InRange(distributionData.Max, 9.9 - TOLERANCE, 9.9 + TOLERANCE);
            Assert.InRange(distributionData.SumOfSquaredDeviations, 32.2 - TOLERANCE, 32.2 + TOLERANCE);
            Assert.Equal(4, distributionData.BucketCounts[0]);
            Assert.Equal(1, distributionData.BucketCounts[1]);
            Assert.Equal(5, distributionData.BucketCounts[2]);
        }

        [Fact]
        public void PreventNullBucketCountList()
        {
            // thrown.expect(NullPointerException.class);
            // thrown.expectMessage("bucket counts should not be null.");
            Assert.Throws<ArgumentNullException>(() => DistributionData.Create(1, 1, 1, 1, 0, null));
        }

        [Fact]
        public void PreventMinIsGreaterThanMax()
        {
            // thrown.expect(IllegalArgumentException.class);
            // thrown.expectMessage("max should be greater or equal to min.");
            Assert.Throws<ArgumentOutOfRangeException>(() => DistributionData.Create(1, 1, 10, 1, 0, new List<long>() { 0L, 1L, 0L }));
        }

        [Fact]
        public void TestEquals()
        {
            var a1 = SumDataDouble.Create(10.0);
            var a2 = SumDataDouble.Create(20.0);
            var a3 = SumDataLong.Create(20);
            var a5 = CountData.Create(40);
            var a6 = CountData.Create(80);
            var a7 = DistributionData.Create(10, 10, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a8 = DistributionData.Create(10, 10, 1, 1, 0, new List<long>() { 0L, 10L, 100L });
            var a9 = DistributionData.Create(110, 10, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a10 = DistributionData.Create(10, 110, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a11 = DistributionData.Create(10, 10, -1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a12 = DistributionData.Create(10, 10, 1, 5, 0, new List<long>() { 0L, 10L, 0L });
            var a13 = DistributionData.Create(10, 10, 1, 1, 55.5, new List<long>() { 0L, 10L, 0L });
            var a14 = MeanData.Create(5.0, 1, 5.0, 5.0);
            var a15 = MeanData.Create(-5.0, 1, -5.0, -5.0);
            var a16 = LastValueDataDouble.Create(20.0);
            var a17 = LastValueDataLong.Create(20);

            var a1a = SumDataDouble.Create(10.0);
            var a2a = SumDataDouble.Create(20.0);
            var a3a = SumDataLong.Create(20);
            var a5a = CountData.Create(40);
            var a6a = CountData.Create(80);
            var a7a = DistributionData.Create(10, 10, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a8a = DistributionData.Create(10, 10, 1, 1, 0, new List<long>() { 0L, 10L, 100L });
            var a9a = DistributionData.Create(110, 10, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a10a = DistributionData.Create(10, 110, 1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a11a = DistributionData.Create(10, 10, -1, 1, 0, new List<long>() { 0L, 10L, 0L });
            var a12a = DistributionData.Create(10, 10, 1, 5, 0, new List<long>() { 0L, 10L, 0L });
            var a13a = DistributionData.Create(10, 10, 1, 1, 55.5, new List<long>() { 0L, 10L, 0L });
            var a14a = MeanData.Create(5.0, 1, 5.0, 5.0);
            var a15a = MeanData.Create(-5.0, 1, -5.0, -5.0);
            var a16a = LastValueDataDouble.Create(20.0);
            var a17a = LastValueDataLong.Create(20);

            Assert.Equal(a1, a1a);
            Assert.Equal(a2, a2a);
            Assert.Equal(a3, a3a);
            Assert.Equal(a5, a5a);
            Assert.Equal(a6, a6a);
            Assert.Equal(a7, a7a);
            Assert.Equal(a8, a8a);
            Assert.Equal(a9, a9a);
            Assert.Equal(a10, a10a);
            Assert.Equal(a11, a11a);
            Assert.Equal(a12, a12a);
            Assert.Equal(a13, a13a);
            Assert.Equal(a14, a14a);
            Assert.Equal(a15, a15a);
            Assert.Equal(a16, a16a);
            Assert.Equal(a17, a17a);

        }

        [Fact]
        public void TestMatchAndGet()
        {
            List<IAggregationData> aggregations =
                new List<IAggregationData>() {
                    SumDataDouble.Create(10.0),
                    SumDataLong.Create(100000000),
                    CountData.Create(40),
                    MeanData.Create(100.0, 10, 300.0, 500.0),
                    DistributionData.Create(1, 1, 1, 1, 0, new List<long>() { 0L, 10L, 0L }),
                    LastValueDataDouble.Create(20.0),
                    LastValueDataLong.Create(200000000L),
                    };

            List<object> actual = new List<object>();
            foreach (IAggregationData aggregation in aggregations)
            {
                aggregation.Match<object>(
                    (arg) =>
                    {
                        actual.Add(arg.Sum);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.Sum);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.Count);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.Mean);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.BucketCounts);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.LastValue);
                        return null;
                    },
                    (arg) =>
                    {
                        actual.Add(arg.LastValue);
                        return null;
                    },
                    (arg) => { throw new ArgumentException(); });
            }
            Assert.Equal(10.0, actual[0]);
            Assert.Equal(100000000L, actual[1]);
            Assert.Equal(40L, actual[2]);
            Assert.Equal(100.0, actual[3]);
            Assert.Equal(new List<long>() { 0L, 10L, 0L }, actual[4]);
            Assert.Equal(20.0, actual[5]);
            Assert.Equal(200000000L, actual[6]);

        }
    }
}

