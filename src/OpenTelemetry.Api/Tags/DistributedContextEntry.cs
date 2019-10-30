// <copyright file="DistributedContextEntry.cs" company="OpenTelemetry Authors">
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
using System;

namespace OpenTelemetry.Tags
{
    /// <summary>
    /// Distributed Context entry with the key, value and metadata.
    /// </summary>
    public sealed class DistributedContextEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContextEntry"/> class with the key and value.
        /// </summary>
         /// <param name="key">Key name for the entry.</param>
        /// <param name="value">Value associated with the key name.</param>
        public DistributedContextEntry(string key, string value)
            : this(key, value, new EntryMetadata(EntryMetadata.NoPropagation))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContextEntry"/> class with the key and value.
        /// </summary>
        /// <param name="key">Key name for the entry.</param>
        /// <param name="value">Value associated with the key name.</param>
        /// <param name="metadata">Entry metadata.</param>
        public DistributedContextEntry(string key, string value, in EntryMetadata metadata)
        {
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
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

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(DistributedContextEntry)
                + "{"
                + nameof(this.Key) + "=" + this.Key + ", "
                + nameof(this.Value) + "=" + this.Value
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is DistributedContextEntry that)
            {
                return this.Key.Equals(that.Key)
                     && this.Value.Equals(that.Value);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Key.GetHashCode();
            h *= 1000003;
            h ^= this.Value.GetHashCode();
            return h;
        }
    }
}
