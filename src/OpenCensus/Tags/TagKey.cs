// <copyright file="TagKey.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Tags
{
    using System;
    using OpenCensus.Utils;

    public sealed class TagKey : ITagKey
    {
        public const int MaxLength = 255;

        internal TagKey(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }

        public static ITagKey Create(string name)
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
            int h = 1;
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
