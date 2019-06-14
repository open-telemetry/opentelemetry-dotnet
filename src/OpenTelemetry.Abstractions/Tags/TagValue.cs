// <copyright file="TagValue.cs" company="OpenTelemetry Authors">
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
    public sealed class TagValue
    {
        /// <summary>
        /// Maximum string length of the value.
        /// </summary>
        public const int MaxLength = 255;

        internal TagValue(string asString)
        {
            this.AsString = asString ?? throw new ArgumentNullException(nameof(asString));
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string AsString { get; }

        /// <summary>
        /// Creates a new <see cref="TagValue"/> from the given value.
        /// </summary>
        /// <param name="value">The tag's value.</param>
        /// <returns><see cref="TagValue"/>.</returns>
        public static TagValue Create(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new TagValue(value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TagValue{"
                + "asString=" + this.AsString
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TagValue that)
            {
                return this.AsString.Equals(that.AsString);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.AsString.GetHashCode();
            return h;
        }

        private static bool IsValid(string value)
        {
            return value.Length <= MaxLength && StringUtil.IsPrintableString(value);
        }
    }
}
