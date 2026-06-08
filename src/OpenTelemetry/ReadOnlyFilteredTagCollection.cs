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
public readonly struct ReadOnlyFilteredTagCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
#if NET
    private readonly FrozenSet<string>? includeKeys;
    private readonly FrozenSet<string>? excludeKeys;
#else
    private readonly HashSet<string>? includeKeys;
    private readonly HashSet<string>? excludeKeys;
#endif
    private readonly KeyValuePair<string, object?>[] tags;

    internal ReadOnlyFilteredTagCollection(
#if NET
        FrozenSet<string>? includeKeys,
        FrozenSet<string>? excludeKeys,
#else
        HashSet<string>? includeKeys,
        HashSet<string>? excludeKeys,
#endif
        KeyValuePair<string, object?>[] tags,
        int count)
    {
        this.includeKeys = includeKeys;
        this.excludeKeys = excludeKeys;
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
            var maximumCount = this.source.MaximumCount;
            var includeKeys = this.source.includeKeys;
            var excludeKeys = this.source.excludeKeys;
            var tags = this.source.tags;

            while (true)
            {
                var index = ++this.index;
                if (index < maximumCount)
                {
                    if (includeKeys != null)
                    {
                        // Include mode: skip tags that were kept (not filtered)
                        if (includeKeys.Contains(tags[index].Key))
                        {
                            continue;
                        }

                        return true;
                    }
                    else if (excludeKeys != null)
                    {
                        // Exclude mode: yield only tags that were dropped (filtered)
                        if (!excludeKeys.Contains(tags[index].Key))
                        {
                            continue;
                        }

                        return true;
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
