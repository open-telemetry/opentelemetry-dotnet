// <copyright file="CircularBufferBucketsTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class CircularBufferBucketsTest
{
    [Fact]
    public void Constructor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(-1));
    }

    [Fact]
    public void BasicInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.Equal(5, buckets.Capacity);
        Assert.Equal(0, buckets.Size);

        Assert.True(buckets.TryIncrement(0));
        Assert.Equal(1, buckets.Size);

        Assert.True(buckets.TryIncrement(1));
        Assert.Equal(2, buckets.Size);

        Assert.True(buckets.TryIncrement(3));
        Assert.Equal(4, buckets.Size);

        Assert.True(buckets.TryIncrement(4));
        Assert.Equal(5, buckets.Size);

        Assert.True(buckets.TryIncrement(2));
        Assert.Equal(5, buckets.Size);

        Assert.False(buckets.TryIncrement(5));
        Assert.False(buckets.TryIncrement(-1));
        Assert.Equal(5, buckets.Size);
    }

    [Fact]
    public void PositiveInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.True(buckets.TryIncrement(102));
        Assert.True(buckets.TryIncrement(103));
        Assert.True(buckets.TryIncrement(101));
        Assert.True(buckets.TryIncrement(100));
        Assert.True(buckets.TryIncrement(104));

        Assert.False(buckets.TryIncrement(99));
        Assert.False(buckets.TryIncrement(105));
    }

    [Fact]
    public void NegativeInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.True(buckets.TryIncrement(2));
        Assert.True(buckets.TryIncrement(0));
        Assert.True(buckets.TryIncrement(-2));
        Assert.True(buckets.TryIncrement(1));
        Assert.True(buckets.TryIncrement(-1));

        Assert.False(buckets.TryIncrement(3));
        Assert.False(buckets.TryIncrement(-3));
    }

    [Fact]
    public void IndexOperations()
    {
        var buckets = new CircularBufferBuckets(5);

        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(0);
        buckets.TryIncrement(0);
        buckets.TryIncrement(0);
        buckets.TryIncrement(-2);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(-1);
        buckets.TryIncrement(-1);

        Assert.Equal(1, buckets[-2]);
        Assert.Equal(2, buckets[-1]);
        Assert.Equal(3, buckets[0]);
        Assert.Equal(4, buckets[1]);
        Assert.Equal(5, buckets[2]);
    }
}
