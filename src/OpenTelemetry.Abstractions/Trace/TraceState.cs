// <copyright file="TraceState.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Tracestate entries allowing different vendors to participate in a trace.
    /// See https://github.com/w3c/distributed-tracing.
    /// </summary>
    public sealed class Tracestate
    {
        /// <summary>
        /// An instance of empty tracestate.
        /// </summary>
        public static readonly Tracestate Empty = new Tracestate(Enumerable.Empty<Entry>());

        private const int KeyMaxSize = 256;
        private const int ValueMaxSize = 256;
        private const int MaxKeyValuePairsCount = 32;

        private readonly IEnumerable<Entry> entries;

        private Tracestate(IEnumerable<Entry> entries)
        {
            this.entries = entries;
        }

        /// <summary>
        /// Gets the tracestate builder.
        /// </summary>
        public static TracestateBuilder Builder
        {
            get
            {
                return new TracestateBuilder(Tracestate.Empty);
            }
        }

        /// <summary>
        /// Gets the list of entris in tracestate.
        /// </summary>
        public IEnumerable<Entry> Entries { get => this.entries; }

        /// <summary>
        /// Returns the value to which the specified key is mapped, or null if this map contains no mapping
        /// for the key.
        /// </summary>
        /// <param name="key">Key with which the specified value is to be associated.</param>
        /// <returns>
        /// the value to which the specified key is mapped, or null if this map contains no mapping
        /// for the key.
        /// </returns>
        public string Get(string key)
        {
            foreach (var entry in this.Entries)
            {
                if (entry.Key.Equals(key))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the builder to create derived tracestate.
        /// </summary>
        /// <returns>Tracestate builder.</returns>
        public TracestateBuilder ToBuilder()
        {
            return new TracestateBuilder(this);
        }

        private static bool ValidateKey(string key)
        {
            // Key is opaque string up to 256 characters printable. It MUST begin with a lowercase letter, and
            // can only contain lowercase letters a-z, digits 0-9, underscores _, dashes -, asterisks *,
            // forward slashes / and @

            var i = 0;

            if (string.IsNullOrEmpty(key)
                || key.Length > KeyMaxSize
                || key[i] < 'a'
                || key[i] > 'z')
            {
                return false;
            }

            // before
            for (i = 1; i < key.Length; i++)
            {
                var c = key[i];

                if (c == '@')
                {
                    // vendor follows
                    break;
                }

                if (!(c >= 'a' && c <= 'z')
                    && !(c >= '0' && c <= '9')
                    && c != '_'
                    && c != '-'
                    && c != '*'
                    && c != '/')
                {
                    return false;
                }
            }

            i++; // skip @ or increment further than key.Length

            var vendorLength = key.Length - i;
            if (vendorLength == 0 || vendorLength > 14)
            {
                // vendor name should be at least 1 to 14 character long
                return false;
            }

            if (vendorLength > 0)
            {
                if (i > 242)
                {
                    // tenant section should be less than 241 characters long
                    return false;
                }
            }

            for (; i < key.Length; i++)
            {
                var c = key[i];

                if (!(c >= 'a' && c <= 'z')
                    && !(c >= '0' && c <= '9')
                    && c != '_'
                    && c != '-'
                    && c != '*'
                    && c != '/')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateValue(string value)
        {
            // Value is opaque string up to 256 characters printable ASCII RFC0020 characters (i.e., the range
            // 0x20 to 0x7E) except comma , and =.

            if (value.Length > ValueMaxSize || value[value.Length - 1] == ' ' /* '\u0020' */)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == ',' || c == '=' || c < ' ' /* '\u0020' */ || c > '~' /* '\u007E' */)
                {
                    return false;
                }
            }

            return true;
        }

        private static Tracestate Create(ICollection<Entry> entries)
        {
            // TODO: discard last entries instead of throwing

            if (entries.Count > MaxKeyValuePairsCount)
            {
                throw new ArgumentException("Too many entries.", nameof(entries));
            }

            return new Tracestate(entries);
        }

        /// <summary>
        /// Immutable tracestate entry.
        /// </summary>
        public sealed class Entry
        {
            private readonly string key;
            private readonly string value;

            private Entry(string key, string value)
            {
                this.key = key;
                this.value = value;
            }

            /// <summary>
            /// Gets the key of tracestate entry.
            /// </summary>
            public string Key { get => this.key; }

            /// <summary>
            /// Gets the value of tracestate entry.
            /// </summary>
            public string Value { get => this.value; }

            /// <summary>
            /// Creates a new Entry with the given name and value.
            /// </summary>
            /// <param name="key">Key of tracestate entry.</param>
            /// <param name="value">Value of thacestate entry.</param>
            /// <returns>The new tracestate entry.</returns>
            public static Entry Create(string key, string value)
            {
                key = key ?? throw new ArgumentNullException(nameof(key));
                value = value ?? throw new ArgumentNullException(nameof(value));

                if (!ValidateKey(key))
                {
                    throw new ArgumentException("Doesn't comply to spec", nameof(key));
                }

                if (!ValidateValue(value))
                {
                    throw new ArgumentException("Doesn't comply to spec", nameof(value));
                }

                return new Entry(key, value);
            }
        }

        /// <summary>
        /// Tracestate builder.
        /// </summary>
        public sealed class TracestateBuilder
        {
            private readonly Tracestate parent;

            private IList<Entry> entries;

            internal TracestateBuilder(Tracestate parent)
            {
                parent = parent ?? throw new ArgumentNullException(nameof(parent));

                this.parent = parent;
                this.entries = null;
            }

            /// <summary>
            /// Adds or updates the entry for the given key.
            /// New or updated entry will be moved to the front of the list.
            /// </summary>
            /// <param name="key">Key to update value for.</param>
            /// <param name="value">Value set for the key.</param>
            /// <returns>Tracestate builder for chained calls.</returns>
            public TracestateBuilder Set(string key, string value)
            {
                // Initially create the Entry to validate input.

                var entry = Entry.Create(key, value);

                if (this.entries == null)
                {
                    // Copy entries from the parent.
                    this.entries = new List<Entry>(this.parent.Entries);
                }

                for (var i = 0; i < this.entries.Count; i++)
                {
                    if (this.entries[i].Key.Equals(entry.Key))
                    {
                        this.entries.RemoveAt(i);

                        // Exit now because the entries list cannot contain duplicates.
                        break;
                    }
                }

                // Inserts the element at the front of this list.
                this.entries.Insert(0, entry);
                return this;
            }

            /// <summary>
            /// Removes entry for the given key.
            /// </summary>
            /// <param name="key">Key to remove from tracestate.</param>
            /// <returns>Tracestate builder for chained calls.</returns>
            public TracestateBuilder Remove(string key)
            {
                key = key ?? throw new ArgumentNullException(nameof(key));

                if (this.entries == null)
                {
                    // Copy entries from the parent.
                    this.entries = new List<Entry>(this.parent.Entries);
                }

                for (var i = 0; i < this.entries.Count; i++)
                {
                    if (this.entries[i].Key.Equals(key))
                    {
                        this.entries.RemoveAt(i);

                        // Exit now because the entries list cannot contain duplicates.
                        break;
                    }
                }

                return this;
            }

            /// <summary>
            /// Builds the tracestate.
            /// </summary>
            /// <returns>Resulting tracestate.</returns>
            public Tracestate Build()
            {
                if (this.entries == null)
                {
                    return this.parent;
                }

                return Tracestate.Create(this.entries);
            }
        }
    }
}
