// <copyright file="Tag.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tags
{
    using System;

    /// <summary>
    /// Tag with the key and value.
    /// </summary>
    public sealed class Tag
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class with the key and value.
        /// </summary>
        /// <param name="key">Key name for the tag.</param>
        /// <param name="value">Value associated with the key name.</param>
        internal Tag(TagKey key, TagValue value)
        {
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets the tag key.
        /// </summary>
        public TagKey Key { get; }

        /// <summary>
        /// Gets the tag value.
        /// </summary>
        public TagValue Value { get; }

        /// <summary>
        /// Creates a new <see cref="Tag"/> from the given key and value.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns><see cref="Tag"/>.</returns>
        public static Tag Create(TagKey key, TagValue value)
        {
            return new Tag(key, value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Tag{"
                + "key=" + this.Key + ", "
                + "value=" + this.Value
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Tag that)
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
