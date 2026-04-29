// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif

namespace OpenTelemetry;

/// <summary>
/// A read-only collection of tag key/value pairs which returns a filtered
/// subset of tags when enumerated.
/// </summary>
// Note: Does not implement IReadOnlyCollection<> or IEnumerable<> to
// prevent accidental boxing.
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public readonly struct ReadOnlyFilteredTagCollection : IEquatable<ReadOnlyFilteredTagCollection>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
#if NET
    private readonly FrozenSet<string>? excludedKeys;
#else
    private readonly HashSet<string>? excludedKeys;
#endif
    private readonly KeyValuePair<string, object?>[] tags;

    internal ReadOnlyFilteredTagCollection(
#if NET
        FrozenSet<string>? excludedKeys,
#else
        HashSet<string>? excludedKeys,
#endif
        KeyValuePair<string, object?>[] tags,
        int count)
    {
        this.excludedKeys = excludedKeys;
        this.tags = tags;
        this.MaximumCount = count;
    }

    /// <summary>
    /// Gets the maximum number of tags in the collection.
    /// </summary>
    /// <remarks>
    /// Note: Enumerating the collection may return fewer results depending on
    /// the filter.
    /// </remarks>
    internal int MaximumCount { get; }

    /// <summary>
    /// Returns an enumerator that iterates through the tags.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Compare two <see cref="ReadOnlyFilteredTagCollection"/> for equality.
    /// </summary>
    public static bool operator ==(ReadOnlyFilteredTagCollection left, ReadOnlyFilteredTagCollection right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="ReadOnlyFilteredTagCollection"/> for inequality.
    /// </summary>
    public static bool operator !=(ReadOnlyFilteredTagCollection left, ReadOnlyFilteredTagCollection right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ReadOnlyFilteredTagCollection other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(ReadOnlyFilteredTagCollection other)
        => ReferenceEquals(this.excludedKeys, other.excludedKeys)
        && ReferenceEquals(this.tags, other.tags)
        && this.MaximumCount == other.MaximumCount;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.excludedKeys, this.tags, this.MaximumCount);
#else
        unchecked
        {
            var hash = 17;
            hash = (31 * hash) + (this.excludedKeys?.GetHashCode() ?? 0);
            hash = (31 * hash) + (this.tags?.GetHashCode() ?? 0);
            hash = (31 * hash) + this.MaximumCount.GetHashCode();
            return hash;
        }
#endif
    }

    internal IReadOnlyList<KeyValuePair<string, object?>> ToReadOnlyList()
    {
        var list = new List<KeyValuePair<string, object?>>(this.MaximumCount);

        foreach (var item in this)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="ReadOnlyTagCollection"/>.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        private readonly ReadOnlyFilteredTagCollection source;
        private int index;

        internal Enumerator(ReadOnlyFilteredTagCollection source)
        {
            this.source = source;
            this.index = -1;
        }

        /// <summary>
        /// Gets the tag at the current position of the enumerator.
        /// </summary>
        public readonly KeyValuePair<string, object?> Current
            => this.source.tags[this.index];

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="ReadOnlyTagCollection"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
        {
            while (true)
            {
                var index = ++this.index;
                if (index < this.source.MaximumCount)
                {
                    if (this.source.excludedKeys?.Contains(this.source.tags[index].Key) ?? false)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
