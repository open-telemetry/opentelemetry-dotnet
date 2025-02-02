// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A read-only collection of <see cref="Exemplar" />s.
/// </summary>
public readonly struct ReadOnlyExemplarCollection
{
    internal static readonly ReadOnlyExemplarCollection Empty = new(Array.Empty<Exemplar>());
    private readonly Exemplar[] exemplars;

    internal ReadOnlyExemplarCollection(Exemplar[] exemplars)
    {
        this.exemplars = exemplars;
    }

    /// <summary>
    /// Gets the maximum number of <see cref="Exemplar" />s in the collection.
    /// </summary>
    /// <remarks>
    /// Note: Enumerating the collection may return fewer results depending on
    /// which <see cref="Exemplar"/>s in the collection received updates.
    /// </remarks>
    internal int MaximumCount => this.exemplars.Length;

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Exemplar" />s.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator()
        => new(this.exemplars);

    internal ReadOnlyExemplarCollection Copy()
    {
        var maximumCount = this.MaximumCount;

        if (maximumCount > 0)
        {
            var exemplarCopies = new Exemplar[maximumCount];

            int i = 0;
            foreach (ref readonly var exemplar in this)
            {
                if (exemplar.IsUpdated())
                {
                    exemplar.Copy(ref exemplarCopies[i++]);
                }
            }

            return new ReadOnlyExemplarCollection(exemplarCopies);
        }

        return Empty;
    }

    internal IReadOnlyList<Exemplar> ToReadOnlyList()
    {
        var list = new List<Exemplar>(this.MaximumCount);

        foreach (var exemplar in this)
        {
            // Note: If ToReadOnlyList is ever made public it should make sure
            // to take copies of exemplars or make sure the instance was first
            // copied using the Copy method above.
            list.Add(exemplar);
        }

        return list;
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="ReadOnlyExemplarCollection"/>.
    /// </summary>
    public struct Enumerator
    {
        private readonly Exemplar[] exemplars;
        private int index;

        internal Enumerator(Exemplar[] exemplars)
        {
            this.exemplars = exemplars;
            this.index = -1;
        }

        /// <summary>
        /// Gets the <see cref="Exemplar"/> at the current position of the enumerator.
        /// </summary>
        public readonly ref readonly Exemplar Current
            => ref this.exemplars[this.index];

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="ReadOnlyExemplarCollection"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
        {
            var exemplars = this.exemplars;

            while (true)
            {
                var index = ++this.index;
                if (index < exemplars.Length)
                {
                    if (!exemplars[index].IsUpdated())
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
