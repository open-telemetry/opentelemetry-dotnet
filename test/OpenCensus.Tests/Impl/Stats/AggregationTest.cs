// <copyright file="AggregationTest.cs" company="OpenCensus Authors">
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

    public class AggregationTest
    {
        [Fact]
        public void TestCreateDistribution()
        {
            IBucketBoundaries bucketBoundaries = BucketBoundaries.Create(new List<double>() { 0.1, 2.2, 33.3 });
            IDistribution distribution = Distribution.Create(bucketBoundaries);
            Assert.Equal(bucketBoundaries, distribution.BucketBoundaries);
        }

        [Fact]
        public void TestNullBucketBoundaries()
        {
            Assert.Throws<ArgumentNullException>(() => Distribution.Create(null)); ;
        }

        [Fact]
        public void TestEquals()
        {
            IAggregation a1 = Sum.Create();
            IAggregation a2 = Sum.Create();

            IAggregation a3 = Count.Create();
            IAggregation a4 = Count.Create();

            IAggregation a5 = Distribution.Create(BucketBoundaries.Create(new List<double>() { -10.0, 1.0, 5.0 }));
            IAggregation a6 = Distribution.Create(BucketBoundaries.Create(new List<double>() { -10.0, 1.0, 5.0 }));

            IAggregation a7 = Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 1.0, 5.0 }));
            IAggregation a8 = Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 1.0, 5.0 }));

            IAggregation a9 = Mean.Create();
            IAggregation a10 = Mean.Create();

            IAggregation a11 = LastValue.Create();
            IAggregation a12 = LastValue.Create();

            Assert.Equal(a1, a2);
            Assert.Equal(a3, a4);
            Assert.Equal(a5, a6);
            Assert.Equal(a7, a8);
            Assert.Equal(a9, a10);
            Assert.Equal(a11, a12);

        }

        [Fact]
        public void TestMatch()
        {
            List<IAggregation> aggregations =
                new List<IAggregation>() {
                                Sum.Create(),
                                Count.Create(),
                                Mean.Create(),
                                Distribution.Create(BucketBoundaries.Create(new List<double>() {-10.0, 1.0, 5.0 })),
                                LastValue.Create(),};
            List<String> actual = new List<String>();
            foreach (IAggregation aggregation in aggregations)
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
                        },
                        (arg) =>
                        {
                            throw new ArgumentException();
                        }));

            }
            Assert.Equal(new List<string>() { "SUM", "COUNT", "MEAN", "DISTRIBUTION", "LASTVALUE" }, actual);
        }
    }
}
