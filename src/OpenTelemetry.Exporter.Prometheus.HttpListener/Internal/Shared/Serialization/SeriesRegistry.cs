// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Keeps track of the series (a sample name together with its labels) that have been written to a scrape response,
/// so that a series is never written twice.
/// </summary>
/// <remarks>
/// A series which appears more than once makes the whole response invalid, and it is then rejected in its entirety.
/// Different metrics (and different measurements of the same metric) can be exposed as the same series, such as when
/// the names of instruments, or of tags, are the same once they have been escaped, when the scope information
/// which would tell them apart has been disabled, or when the name of one metric is that of a sample which is
/// generated for another (such as a gauge named <c>h_sum</c> and a histogram named <c>h</c>).
/// <para/>
/// The identity of a series does not depend on the order of its labels, so series with the same labels in a different
/// order are the same series. The identities are copied, in that form, to a buffer owned by the registry, so
/// that they do not depend on what is later written to the response buffer. Series that have been removed from the
/// response must be removed using <see cref="Rollback"/>. This type is not thread-safe.
/// </remarks>
internal sealed class SeriesRegistry
{
    private const int InitialCapacity = 64;
    private const int InitialArenaLength = 4 * 1024;
    private const int MaximumRetainedCapacity = 64 * 1024;
    private const int MaximumRetainedArenaLength = 16 * 1024 * 1024;

    // The hash is seeded, so that the hash codes of the series (which include values that may come
    // from untrusted input) cannot be chosen to make all of them collide.
    private readonly ulong seed = (ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL;

    private byte[] arena = new byte[InitialArenaLength];
    private int arenaLength;

    private int[] starts = new int[InitialCapacity];
    private int[] lengths = new int[InitialCapacity];
    private int[] hashes = new int[InitialCapacity];
    private int[] next = new int[InitialCapacity];

    // The index (plus one, so that zero is empty) of the last entry that was added to each bucket.
    private int[] buckets = new int[InitialCapacity];

    // The positions of the labels of the series that is being added.
    private int[] labelStarts = new int[16];
    private int[] labelLengths = new int[16];

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

        if (this.arena.Length > MaximumRetainedArenaLength)
        {
            this.arena = new byte[InitialArenaLength];
        }

        this.arenaLength = 0;
        this.Count = 0;
        this.DroppedCount = 0;
        this.FirstDroppedMetricName = null;
    }

    /// <summary>
    /// Adds a series, unless it has already been added.
    /// </summary>
    /// <param name="buffer">The buffer containing the series.</param>
    /// <param name="start">The position in <paramref name="buffer"/> of the first byte of the series.</param>
    /// <param name="length">
    /// The length in bytes of the series, which is a sample name followed by its labels in braces (or
    /// the name quoted inside the braces, if it needs to be). Anything after the closing brace (such as the value of
    /// the sample) is ignored, and the closing brace and the last label may be missing (such as the
    /// prefix of the name and labels of a bucket), in which case the series is what there is.
    /// </param>
    /// <returns><see langword="true"/> if the series was added; <see langword="false"/> if it is a duplicate.</returns>
    public bool TryAdd(byte[] buffer, int start, int length)
    {
        var identityStart = this.arenaLength;
        var identityLength = this.AppendIdentity(new ReadOnlySpan<byte>(buffer, start, length));
        var identity = new ReadOnlySpan<byte>(this.arena, identityStart, identityLength);

        var hash = this.GetHash(identity);

        for (var entry = this.buckets[GetBucket(hash, this.buckets.Length)]; entry != 0; entry = this.next[entry - 1])
        {
            var index = entry - 1;

            if (this.hashes[index] == hash &&
                this.lengths[index] == identityLength &&
                new ReadOnlySpan<byte>(this.arena, this.starts[index], identityLength).SequenceEqual(identity))
            {
                // It is a duplicate, so its identity does not need to be kept.
                this.arenaLength = identityStart;
                return false;
            }
        }

        if (this.Count == this.starts.Length)
        {
            this.Grow();
        }

        var bucket = GetBucket(hash, this.buckets.Length);
        var added = this.Count++;

        this.starts[added] = identityStart;
        this.lengths[added] = identityLength;
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
        if (count >= this.Count)
        {
            return;
        }

        this.arenaLength = this.starts[count];

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

    // Copies the name of a series, and its labels in order, to the end of the arena, and returns the length that was copied.
    private int AppendIdentity(ReadOnlySpan<byte> series)
    {
        // A name which needs to be quoted is written as the first element inside the braces.
        var isQuotedName = series.Length > 0 && series[0] == unchecked((byte)'{');

        var nameLength = 0;
        var index = 0;

        if (isQuotedName)
        {
            index = 1;
        }
        else
        {
            while (index < series.Length && series[index] != unchecked((byte)'{'))
            {
                index++;
            }

            nameLength = index;

            if (index < series.Length)
            {
                index++;
            }
        }

        var labelCount = this.FindLabels(series, index);

        // The name of a quoted series is its first element, and so is not ordered with the labels.
        var firstLabel = isQuotedName ? 1 : 0;

        this.SortLabels(series, firstLabel, labelCount);

        this.EnsureArenaCapacity(series.Length + 4);

        var start = this.arenaLength;
        var cursor = start;

        if (isQuotedName)
        {
            this.arena[cursor++] = unchecked((byte)'{');

            if (labelCount > 0)
            {
                series.Slice(this.labelStarts[0], this.labelLengths[0]).CopyTo(new Span<byte>(this.arena, cursor, this.labelLengths[0]));
                cursor += this.labelLengths[0];
            }
        }
        else
        {
            series.Slice(0, nameLength).CopyTo(new Span<byte>(this.arena, cursor, nameLength));
            cursor += nameLength;
            this.arena[cursor++] = unchecked((byte)'{');
        }

        for (var label = firstLabel; label < labelCount; label++)
        {
            if (label > 0)
            {
                this.arena[cursor++] = unchecked((byte)',');
            }

            var length = this.labelLengths[label];

            series.Slice(this.labelStarts[label], length).CopyTo(new Span<byte>(this.arena, cursor, length));
            cursor += length;
        }

        this.arena[cursor++] = unchecked((byte)'}');

        this.arenaLength = cursor;

        return cursor - start;
    }

    // Finds the labels of a series, which end at the closing brace, or at the end if there isn't one.
    private int FindLabels(ReadOnlySpan<byte> series, int index)
    {
        var count = 0;
        var labelStart = index;
        var inQuotes = false;

        for (; index < series.Length; index++)
        {
            var b = series[index];

            if (inQuotes)
            {
                if (b == unchecked((byte)'\\'))
                {
                    // The character that is escaped is skipped, in case it is a quotation mark.
                    index++;
                }
                else if (b == unchecked((byte)'"'))
                {
                    inQuotes = false;
                }

                continue;
            }

            if (b == unchecked((byte)'"'))
            {
                inQuotes = true;
            }
            else if (b == unchecked((byte)','))
            {
                this.AddLabel(ref count, labelStart, index - labelStart);
                labelStart = index + 1;
            }
            else if (b == unchecked((byte)'}'))
            {
                this.AddLabel(ref count, labelStart, index - labelStart);
                return count;
            }
        }

        // There was no closing brace, so the last label, if there is one, is what is left.
        this.AddLabel(ref count, labelStart, Math.Max(series.Length - labelStart, 0));

        return count;
    }

    private void AddLabel(ref int count, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        if (count == this.labelStarts.Length)
        {
            Array.Resize(ref this.labelStarts, count * 2);
            Array.Resize(ref this.labelLengths, count * 2);
        }

        this.labelStarts[count] = start;
        this.labelLengths[count] = length;
        count++;
    }

    // Sorts the labels, using an insertion sort as there are few labels, and they are usually in order already.
    private void SortLabels(ReadOnlySpan<byte> series, int first, int count)
    {
        for (var i = first + 1; i < count; i++)
        {
            var start = this.labelStarts[i];
            var length = this.labelLengths[i];
            var label = series.Slice(start, length);

            var j = i - 1;

            while (j >= first && series.Slice(this.labelStarts[j], this.labelLengths[j]).SequenceCompareTo(label) > 0)
            {
                this.labelStarts[j + 1] = this.labelStarts[j];
                this.labelLengths[j + 1] = this.labelLengths[j];
                j--;
            }

            this.labelStarts[j + 1] = start;
            this.labelLengths[j + 1] = length;
        }
    }

    private void EnsureArenaCapacity(int additionalLength)
    {
        var required = this.arenaLength + additionalLength;

        if (required > this.arena.Length)
        {
            Array.Resize(ref this.arena, Math.Max(this.arena.Length * 2, required));
        }
    }

    private int GetHash(ReadOnlySpan<byte> identity)
    {
        // A multiplicative hash of the identity, which is read a word at a time, as it is the same
        // few hundred bytes that are hashed for every series that is written to a response.
        const ulong Multiplier = 0x9E3779B97F4A7C15UL;

        var hash = this.seed ^ ((ulong)identity.Length * Multiplier);
        var index = 0;

        for (; index + sizeof(ulong) <= identity.Length; index += sizeof(ulong))
        {
            hash = (hash ^ BinaryPrimitives.ReadUInt64LittleEndian(identity.Slice(index))) * Multiplier;
            hash ^= hash >> 29;
        }

        for (; index < identity.Length; index++)
        {
            hash = (hash ^ identity[index]) * Multiplier;
        }

        hash ^= hash >> 32;

        return (int)hash;
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
