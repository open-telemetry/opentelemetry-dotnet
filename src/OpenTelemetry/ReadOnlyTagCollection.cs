// <copyright file="ReadOnlyTagCollection.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

namespace OpenTelemetry
{
    /// <summary>
    /// A read-only collection of tag key/value pairs.
    /// </summary>
    // Note: Does not implement IReadOnlyCollection<> or IEnumerable<> to
    // prevent accidental boxing.
    public readonly struct ReadOnlyTagCollection
    {
        internal readonly KeyValuePair<string, object>[] KeyAndValues;

        internal ReadOnlyTagCollection(KeyValuePair<string, object>[]? keyAndValues)
        {
            this.KeyAndValues = keyAndValues ?? Array.Empty<KeyValuePair<string, object>>();
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
            public KeyValuePair<string, object> Current { get; private set; }

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
                int index = this.index;

                if (index < this.source.Count)
                {
                    this.Current = this.source.KeyAndValues[index];

                    this.index++;
                    return true;
                }

                return false;
            }
        }
    }
}
