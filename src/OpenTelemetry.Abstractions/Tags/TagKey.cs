// <copyright file="TagKey.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Utils;

    /// <summary>
    /// Tag key.
    /// </summary>
    public sealed class TagKey
    {
        /// <summary>
        /// Maximum string length of the key.
        /// </summary>
        public const int MaxLength = 255;

        internal TagKey(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new <see cref="TagKey"/> from the given name.
        /// </summary>
        /// <param name="name">The tag's name.</param>
        /// <returns><see cref="TagKey"/>.</returns>
        public static TagKey Create(string name)
        {
            if (!IsValid(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }

            return new TagKey(name);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TagKey{"
                + "name=" + this.Name
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TagKey that)
            {
                return this.Name.Equals(that.Name);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            return h;
        }

        private static bool IsValid(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Length <= MaxLength && StringUtil.IsPrintableString(value);
        }
    }
}
