// <copyright file="BucketBoundariesTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class BucketBoundariesTest
    {

        [Fact]
        public void TestConstructBoundaries()
        {
            var buckets = new List<double>() { 0.0, 1.0, 2.0 };
            var bucketBoundaries = BucketBoundaries.Create(buckets);
            Assert.Equal(buckets, bucketBoundaries.Boundaries);
        }

        [Fact]
        public void TestBoundariesDoesNotChangeWithOriginalList()
        {
            var original = new List<double>();
            original.Add(0.0);
            original.Add(1.0);
            original.Add(2.0);
            var bucketBoundaries = BucketBoundaries.Create(original);
            original[2] = 3.0;
            original.Add(4.0);
            var expected = new List<double>() { 0.0, 1.0, 2.0 };
            Assert.NotEqual(original, bucketBoundaries.Boundaries);
            Assert.Equal(expected, bucketBoundaries.Boundaries);
        }

        [Fact]
        public void TestNullBoundaries()
        {
            Assert.Throws<ArgumentNullException>(() => BucketBoundaries.Create(null));
        }

        [Fact]
        public void TestUnsortedBoundaries()
        {
            var buckets = new List<double>() { 0.0, 1.0, 1.0 };
            Assert.Throws<ArgumentOutOfRangeException>(() => BucketBoundaries.Create(buckets));
        }

        [Fact]
        public void TestNoBoundaries()
        {
            var buckets = new List<double>();
            var bucketBoundaries = BucketBoundaries.Create(buckets);
            Assert.Equal(buckets, bucketBoundaries.Boundaries);
        }

        [Fact]
        public void TestBucketBoundariesEquals()
        {
            var b1 = BucketBoundaries.Create(new List<double>() { -1.0, 2.0 });
            var b2 = BucketBoundaries.Create(new List<double>() { -1.0, 2.0 });
            var b3 = BucketBoundaries.Create(new List<double>() { -1.0 });
            Assert.Equal(b1, b2);
            Assert.Equal(b3, b3);
            Assert.NotEqual(b1, b3);
            Assert.NotEqual(b2, b3);

        }
    }
}
