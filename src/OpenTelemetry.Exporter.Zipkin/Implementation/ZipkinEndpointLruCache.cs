// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal sealed class ZipkinEndpointLruCache
{
    private readonly int capacity;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> cache;
    private readonly LinkedList<CacheEntry> lruList = new();
    private readonly Lock sync = new();

    public ZipkinEndpointLruCache(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.capacity = capacity;
        this.cache = [with(StringComparer.Ordinal)];
    }

    public int Count
    {
        get
        {
            lock (this.sync)
            {
                return this.cache.Count;
            }
        }
    }

    public ZipkinEndpoint GetOrAdd(string serviceName)
    {
        Guard.ThrowIfNullOrWhitespace(serviceName);

        lock (this.sync)
        {
            if (this.cache.TryGetValue(serviceName, out var existingNode))
            {
                this.lruList.Remove(existingNode);
                this.lruList.AddFirst(existingNode);
                return existingNode.Value.Endpoint;
            }

            var endpoint = ZipkinEndpoint.Create(serviceName);
            var createdNode = new LinkedListNode<CacheEntry>(new CacheEntry(serviceName, endpoint));

            this.lruList.AddFirst(createdNode);
            this.cache[serviceName] = createdNode;

            if (this.cache.Count > this.capacity)
            {
                var nodeToEvict = this.lruList.Last;
                if (nodeToEvict != null)
                {
                    this.lruList.RemoveLast();
                    this.cache.Remove(nodeToEvict.Value.ServiceName);
                }
            }

            return endpoint;
        }
    }

    public void Clear()
    {
        lock (this.sync)
        {
            this.cache.Clear();
            this.lruList.Clear();
        }
    }

    private readonly record struct CacheEntry(string ServiceName, ZipkinEndpoint Endpoint)
    {
        public string ServiceName { get; } = ServiceName;

        public ZipkinEndpoint Endpoint { get; } = Endpoint;
    }
}
