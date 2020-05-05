// <copyright file="CorrelationContextEntry.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed Context entry with the key, value and metadata.
    /// </summary>
    public readonly struct CorrelationContextEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContextEntry"/> struct with the key and value.
        /// </summary>
        /// <param name="key">Key name for the entry.</param>
        /// <param name="value">Value associated with the key name.</param>
        public CorrelationContextEntry(string key, string value)
            : this(key, value, EntryMetadata.NoPropagationEntry)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContextEntry"/> struct with the key, value, and metadata.
        /// </summary>
        /// <param name="key">Key name for the entry.</param>
        /// <param name="value">Value associated with the key name.</param>
        /// <param name="metadata">Entry metadata.</param>
        public CorrelationContextEntry(string key, string value, in EntryMetadata metadata)
        {
            if (key == null)
            {
                this.Key = string.Empty;
                OpenTelemetryApiEventSource.Log.InvalidArgument(nameof(CorrelationContextEntry), nameof(key), "is null");
            }
            else
            {
                this.Key = key;
            }

            if (value == null)
            {
                this.Value = string.Empty;
                OpenTelemetryApiEventSource.Log.InvalidArgument(nameof(CorrelationContextEntry), nameof(value), "is null");
            }
            else
            {
                this.Value = value;
            }

            this.Metadata = metadata;
        }

        /// <summary>
        /// Gets the tag key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the tag value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the metadata associated with this entry.
        /// </summary>
        public EntryMetadata Metadata { get; }

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContextEntry"/> for equality.
        /// </summary>
        /// <param name="entry1">First Entry to compare.</param>
        /// <param name="entry2">Second Entry to compare.</param>
        public static bool operator ==(CorrelationContextEntry entry1, CorrelationContextEntry entry2) => entry1.Equals(entry2);

        /// <summary>
        /// Compare two entries of <see cref="CorrelationContextEntry"/> for not equality.
        /// </summary>
        /// <param name="entry1">First Entry to compare.</param>
        /// <param name="entry2">Second Entry to compare.</param>
        public static bool operator !=(CorrelationContextEntry entry1, CorrelationContextEntry entry2) => !entry1.Equals(entry2);

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            return o is CorrelationContextEntry that && (this.Key == that.Key && this.Value == that.Value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Key is null ? "{}" : $"{nameof(CorrelationContextEntry)}{{{nameof(this.Key)}={this.Key}, {nameof(this.Value)}={this.Value}}}";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.Key is null)
            {
                // Default instance
                return 0;
            }

            var h = 1;
            h *= 1000003;
            h ^= this.Key.GetHashCode();
            h *= 1000003;
            h ^= this.Value.GetHashCode();
            return h;
        }
    }
}
