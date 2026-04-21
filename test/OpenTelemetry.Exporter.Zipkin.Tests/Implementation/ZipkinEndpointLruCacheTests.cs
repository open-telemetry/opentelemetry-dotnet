// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinEndpointLruCacheTests
{
    [Fact]
    public void GetOrAdd_ReturnsSameValueForExistingKey()
    {
        var cache = new ZipkinEndpointLruCache(capacity: 2);

        var first = cache.GetOrAdd("service-a");
        var second = cache.GetOrAdd("service-a");

        Assert.Same(first, second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_EvictsLeastRecentlyUsedEntry()
    {
        var cache = new ZipkinEndpointLruCache(capacity: 2);

        var first = cache.GetOrAdd("service-a");
        _ = cache.GetOrAdd("service-b");
        _ = cache.GetOrAdd("service-c");

        var firstAfterEviction = cache.GetOrAdd("service-a");

        Assert.NotSame(first, firstAfterEviction);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new ZipkinEndpointLruCache(capacity: 3);

        _ = cache.GetOrAdd("service-a");
        _ = cache.GetOrAdd("service-b");

        cache.Clear();

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void GetOrAdd_ForSameKeyCreatesSingleInstanceAcrossThreads()
    {
        var cache = new ZipkinEndpointLruCache(capacity: 8);

        var createdEndpoints = new ConcurrentDictionary<ZipkinEndpoint, byte>();

        Parallel.For(0, 500, _ =>
        {
            var endpoint = cache.GetOrAdd("shared-service");
            createdEndpoints.TryAdd(endpoint, 0);
        });

        Assert.Single(createdEndpoints);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_IsThreadSafeAndStaysBounded()
    {
        var cache = new ZipkinEndpointLruCache(capacity: 64);

        Parallel.For(0, 10_000, i =>
        {
            _ = cache.GetOrAdd($"service-{i % 512}");
        });

        Assert.True(cache.Count <= 64);
    }
}
