// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Keeps track of the series (a metric name together with its labels) that have
/// been written to a scrape response, so that a series is never written twice.
/// </summary>
/// <remarks>
/// A series which appears more than once makes the whole response invalid, and it is then rejected in its entirety.
/// Different metrics (and different measurements of the same metric) can be exposed as the same series, such as when
/// the names of instruments, or of tags, are the same once they have been escaped, or when the scope information
/// which would tell them apart has been disabled.
/// <para/>
/// The series are not copied. They are referenced by their position in the response buffer, which is why
/// the buffer must be provided every time a series is added, and why series that have been removed from the
/// buffer must be removed using <see cref="Rollback"/>. This type is not thread-safe.
/// </remarks>
internal sealed class SeriesRegistry
{
    private const int InitialCapacity = 64;
    private const int MaximumRetainedCapacity = 64 * 1024;

    // The hash is seeded, so that the hash codes of the series (which include values
    // that may come from untrusted input) cannot be chosen to make all of them collide.
    private readonly ulong seed = (ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL;

    private int[] starts = new int[InitialCapacity];
    private int[] lengths = new int[InitialCapacity];
    private int[] hashes = new int[InitialCapacity];
    private int[] next = new int[InitialCapacity];

    // The index (plus one, so that zero is empty) of the last entry that was added to each bucket.
    private int[] buckets = new int[InitialCapacity];

    /// <summary>
    /// Gets the number of series that have been added.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets the number of series that were dropped because they were duplicates.
    /// </summary>
    public int DroppedCount { get; private set; }

    /// <summary>
    /// Gets the name of the first metric to have a series dropped because it was a duplicate, if any.
    /// </summary>
    public string? FirstDroppedMetricName { get; private set; }

    /// <summary>
    /// Removes all of the series, ready for a new response to be written.
    /// </summary>
    public void Clear()
    {
        if (this.starts.Length > MaximumRetainedCapacity)
        {
            this.starts = new int[InitialCapacity];
            this.lengths = new int[InitialCapacity];
            this.hashes = new int[InitialCapacity];
            this.next = new int[InitialCapacity];
            this.buckets = new int[InitialCapacity];
        }
        else
        {
            Array.Clear(this.buckets, 0, this.buckets.Length);
        }

        this.Count = 0;
        this.DroppedCount = 0;
        this.FirstDroppedMetricName = null;
    }

    /// <summary>
    /// Adds a series, unless it has already been added.
    /// </summary>
    /// <param name="buffer">The response buffer containing the series, and those that were added before it.</param>
    /// <param name="start">The position in <paramref name="buffer"/> of the first byte of the series.</param>
    /// <param name="length">The length of the series in bytes.</param>
    /// <returns><see langword="true"/> if the series was added; <see langword="false"/> if it is a duplicate.</returns>
    public bool TryAdd(byte[] buffer, int start, int length)
    {
        var series = new ReadOnlySpan<byte>(buffer, start, length);
        var hash = this.GetHash(series);

        for (var entry = this.buckets[GetBucket(hash, this.buckets.Length)]; entry != 0; entry = this.next[entry - 1])
        {
            var index = entry - 1;

            if (this.hashes[index] == hash &&
                this.lengths[index] == length &&
                new ReadOnlySpan<byte>(buffer, this.starts[index], length).SequenceEqual(series))
            {
                return false;
            }
        }

        if (this.Count == this.starts.Length)
        {
            this.Grow();
        }

        var bucket = GetBucket(hash, this.buckets.Length);
        var added = this.Count++;

        this.starts[added] = start;
        this.lengths[added] = length;
        this.hashes[added] = hash;
        this.next[added] = this.buckets[bucket];
        this.buckets[bucket] = added + 1;

        return true;
    }

    /// <summary>
    /// Removes the series that were added after there were <paramref name="count"/> series, as they were
    /// removed from the response buffer.
    /// </summary>
    /// <param name="count">The <see cref="Count"/> to return to.</param>
    public void Rollback(int count)
    {
        // The series are removed in the reverse of the order they were added in, so each is the head of its bucket.
        for (var index = this.Count - 1; index >= count; index--)
        {
            this.buckets[GetBucket(this.hashes[index], this.buckets.Length)] = this.next[index];
        }

        this.Count = count;
    }

    /// <summary>
    /// Records that a series of a metric was dropped as it was a duplicate.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    public void RecordDropped(string metricName)
    {
        this.DroppedCount++;
        this.FirstDroppedMetricName ??= metricName;
    }

    private static int GetBucket(int hash, int bucketCount) => (hash & int.MaxValue) % bucketCount;

    private int GetHash(ReadOnlySpan<byte> series)
    {
        // FNV-1a
        var hash = this.seed ^ 14695981039346656037UL;

        for (var i = 0; i < series.Length; i++)
        {
            hash ^= series[i];
            hash *= 1099511628211UL;
        }

        return (int)(hash ^ (hash >> 32));
    }

    private void Grow()
    {
        var capacity = this.starts.Length * 2;

        Array.Resize(ref this.starts, capacity);
        Array.Resize(ref this.lengths, capacity);
        Array.Resize(ref this.hashes, capacity);
        Array.Resize(ref this.next, capacity);

        // The buckets are rebuilt in the order the series were added in, as each is the head of its bucket.
        this.buckets = new int[capacity];

        for (var index = 0; index < this.Count; index++)
        {
            var bucket = GetBucket(this.hashes[index], capacity);

            this.next[index] = this.buckets[bucket];
            this.buckets[bucket] = index + 1;
        }
    }
}
