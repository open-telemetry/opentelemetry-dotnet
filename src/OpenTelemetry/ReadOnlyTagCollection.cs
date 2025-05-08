// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// A read-only collection of tag key/value pairs.
/// </summary>
// Note: Does not implement IReadOnlyCollection<> or IEnumerable<> to
// prevent accidental boxing.
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public readonly struct ReadOnlyTagCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    internal readonly KeyValuePair<string, object?>[] KeyAndValues;

    internal ReadOnlyTagCollection(KeyValuePair<string, object?>[]? keyAndValues)
    {
        this.KeyAndValues = keyAndValues ?? [];
    }

    /// <summary>
    /// Gets the number of tags in the collection.
    /// </summary>
    public int Count => this.KeyAndValues.Length;

    /// <summary>
    /// Returns an enumerator that iterates through the tags.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Enumerates the elements of a <see cref="ReadOnlyTagCollection"/>.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        private readonly ReadOnlyTagCollection source;
        private int index;

        internal Enumerator(ReadOnlyTagCollection source)
        {
            this.source = source;
            this.index = -1;
        }

        /// <summary>
        /// Gets the tag at the current position of the enumerator.
        /// </summary>
        public readonly KeyValuePair<string, object?> Current
            => this.source.KeyAndValues[this.index];

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="ReadOnlyTagCollection"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext() => ++this.index < this.source.Count;
    }
}
