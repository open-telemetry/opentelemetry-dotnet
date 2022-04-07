// <copyright file="Tags.cs" company="OpenTelemetry Authors">
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

using System;

namespace OpenTelemetry.Metrics
{
    internal readonly struct Tags : IEquatable<Tags>
    {
        public Tags(string[] keys, object[] values)
        {
            this.Keys = keys;
            this.Values = values;
        }

        public readonly string[] Keys { get; }

        public readonly object[] Values { get; }

        public static bool operator ==(Tags tag1, Tags tag2) => tag1.Equals(tag2);

        public static bool operator !=(Tags tag1, Tags tag2) => !tag1.Equals(tag2);

        public override readonly bool Equals(object obj)
        {
            return obj is Tags other && this.Equals(other);
        }

        public readonly bool Equals(Tags other)
        {
            // Equality check for Keys
            // Check if the two string[] are equal
            var keysLength = this.Keys.Length;

            if (keysLength != other.Keys.Length)
            {
                return false;
            }

            for (int i = 0; i < keysLength; i++)
            {
                if (!this.Keys[i].Equals(other.Keys[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            // Equality check for Values
            // Check if the two object[] are equal
            var valuesLength = this.Values.Length;

            if (valuesLength != other.Values.Length)
            {
                return false;
            }

            for (int i = 0; i < valuesLength; i++)
            {
                if (!this.Values[i].Equals(other.Values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override readonly int GetHashCode()
        {
            int hash = 17;

            unchecked
            {
                for (int i = 0; i < this.Keys.Length; i++)
                {
                    hash = (hash * 31) + this.Keys[i].GetHashCode() + this.Values[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
