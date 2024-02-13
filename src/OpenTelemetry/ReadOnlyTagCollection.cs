// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry;

/// <summary>
/// A read-only collection of tag key/value pairs.
/// </summary>
// Note: Does not implement IReadOnlyCollection<> or IEnumerable<> to
// prevent accidental boxing.
public readonly struct ReadOnlyTagCollection
{
    internal readonly KeyValuePair<string, object?>[] KeyAndValues;
    private readonly HashSet<string>? keyFilter;
    private readonly int count;

    internal ReadOnlyTagCollection(KeyValuePair<string, object?>[]? keyAndValues)
    {
        this.KeyAndValues = keyAndValues ?? Array.Empty<KeyValuePair<string, object?>>();
        this.keyFilter = null;
        this.count = this.KeyAndValues.Length;
    }

    internal ReadOnlyTagCollection(HashSet<string> keyFilter, KeyValuePair<string, object?>[] keyAndValues, int count)
    {
        Debug.Assert(keyFilter != null, "keyFilter was null");
        Debug.Assert(keyAndValues != null, "keyAndValues was null");
        Debug.Assert(count <= keyAndValues.Length, "count was invalid");

        this.keyFilter = keyFilter;
        this.KeyAndValues = keyAndValues;
        this.count = count;
    }

    /// <summary>
    /// Gets the number of tags in the collection.
    /// </summary>
    public int Count => this.count;

    /// <summary>
    /// Returns an enumerator that iterates through the tags.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Enumerates the elements of a <see cref="ReadOnlyTagCollection"/>.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
    public struct Enumerator
    {
        private readonly ReadOnlyTagCollection source;
        private int index;

        internal Enumerator(ReadOnlyTagCollection source)
        {
            this.source = source;
            this.index = 0;
            this.Current = default;
        }

        /// <summary>
        /// Gets the tag at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<string, object?> Current { get; private set; }

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
                int index = this.index;
                if (index < this.source.Count)
                {
                    var item = this.source.KeyAndValues[index++];
                    this.index = index;

                    if (this.source.keyFilter?.Contains(item.Key) == true)
                    {
                        continue;
                    }

                    this.Current = item;
                    return true;
                }

                break;
            }

            return false;
        }
    }
}
