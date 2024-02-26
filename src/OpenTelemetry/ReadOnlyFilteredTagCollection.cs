// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// A read-only collection of tag key/value pairs which returns a filtered
/// subset of tags when enumerated.
/// </summary>
// Note: Does not implement IReadOnlyCollection<> or IEnumerable<> to
// prevent accidental boxing.
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    readonly struct ReadOnlyFilteredTagCollection
{
    private readonly HashSet<string>? excludedKeys;
    private readonly KeyValuePair<string, object?>[] tags;
    private readonly int count;

    internal ReadOnlyFilteredTagCollection(
        HashSet<string>? excludedKeys,
        KeyValuePair<string, object?>[] tags,
        int count)
    {
        Debug.Assert(tags != null, "tags was null");
        Debug.Assert(count <= tags!.Length, "count was invalid");

        this.excludedKeys = excludedKeys;
        this.tags = tags;
        this.count = count;
    }

    /// <summary>
    /// Gets the maximum number of tags in the collection.
    /// </summary>
    /// <remarks>
    /// Note: Enumerating the collection may return fewer results depending on
    /// the filter.
    /// </remarks>
    internal int MaximumCount => this.count;

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
    public struct Enumerator
    {
        private readonly ReadOnlyFilteredTagCollection source;
        private int index;

        internal Enumerator(ReadOnlyFilteredTagCollection source)
        {
            this.source = source;
            this.index = -1;
            this.Current = default;
        }

        /// <summary>
        /// Gets the tag at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<string, object?> Current { readonly get; private set; }

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
                int index = ++this.index;
                if (index < this.source.MaximumCount)
                {
                    var item = this.source.tags[index];

                    if (this.source.excludedKeys?.Contains(item.Key) == true)
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
