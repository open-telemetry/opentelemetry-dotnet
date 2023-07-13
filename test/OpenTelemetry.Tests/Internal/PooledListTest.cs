// <copyright file="PooledListTest.cs" company="OpenTelemetry Authors">
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

using System.Collections;
using System.Reflection;

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class PooledListTest
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
        int GetLastAllocatedSize(PooledList<int> pooledList)
        {
            var value = typeof(PooledList<int>)
                .GetField("lastAllocatedSize", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(pooledList);
            return (int)value;
        }

        var pooledList = PooledList<int>.Create();

        var size = GetLastAllocatedSize(pooledList);
        Assert.Equal(64, size);

        // The Add() method has a condition to double the size of the buffer
        // when the Count exceeds the buffer size.
        // This for loop is meant to trigger that condition.
        for (int i = 0; i <= size; i++)
        {
            PooledList<int>.Add(ref pooledList, i);
        }

        size = GetLastAllocatedSize(pooledList);
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
