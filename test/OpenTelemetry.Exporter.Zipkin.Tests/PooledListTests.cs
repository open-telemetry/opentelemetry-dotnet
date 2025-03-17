// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class PooledListTests
{
    [Fact]
    public void Verify_ICollectionExplicitProperties()
    {
        var pooledList = PooledList<int>.Create();
        var icollection = (ICollection)pooledList;
        Assert.False(icollection.IsSynchronized);
        Assert.Equal(icollection, icollection.SyncRoot);
    }

    [Fact]
    public void Verify_CreateAddClear()
    {
        var pooledList = PooledList<int>.Create();
        Assert.Empty(pooledList);
        Assert.True(pooledList.IsEmpty);

        PooledList<int>.Add(ref pooledList, 1);
        Assert.Single(pooledList);
        Assert.False(pooledList.IsEmpty);

        PooledList<int>.Add(ref pooledList, 2);
        Assert.Equal(2, pooledList.Count);

        Assert.Equal(1, pooledList[0]);
        Assert.Equal(2, pooledList[1]);

        PooledList<int>.Clear(ref pooledList);
        Assert.Empty(pooledList);
        Assert.True(pooledList.IsEmpty);
    }

    [Fact]
    public void Verify_AllocatedSize()
    {
        var pooledList = PooledList<int>.Create();

        var size = PooledList<int>.LastAllocatedSize;
        Assert.Equal(64, size);

        // The Add() method has a condition to double the size of the buffer
        // when the Count exceeds the buffer size.
        // This for loop is meant to trigger that condition.
        for (int i = 0; i <= size; i++)
        {
            PooledList<int>.Add(ref pooledList, i);
        }

        size = PooledList<int>.LastAllocatedSize;
        Assert.Equal(128, size);
    }

    [Fact]
    public void Verify_Enumerator()
    {
        var pooledList = PooledList<int>.Create();
        PooledList<int>.Add(ref pooledList, 1);
        PooledList<int>.Add(ref pooledList, 2);
        PooledList<int>.Add(ref pooledList, 3);

        var enumerator = pooledList.GetEnumerator();

        Assert.Equal(default, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);

        var ienumerator = (IEnumerator)enumerator;
        ienumerator.Reset();
        Assert.Equal(default, enumerator.Current);
    }
}
