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

namespace OpenTelemetry.Metrics
{
    internal readonly struct Tags : IEquatable<Tags>
    {
        private readonly int hashCode;

        public Tags(KeyValuePair<string, object>[] keyValuePairs)
        {
            this.KeyValuePairs = keyValuePairs;

            var hash = 17;
            for (int i = 0; i < this.KeyValuePairs.Length; i++)
            {
                unchecked
                {
                    hash = (hash * 31) + this.KeyValuePairs[i].Key.GetHashCode() + this.KeyValuePairs[i].Value?.GetHashCode() ?? 0;
                }
            }

            this.hashCode = hash;
        }

        public readonly KeyValuePair<string, object>[] KeyValuePairs { get; }

        public static bool operator ==(Tags tag1, Tags tag2) => tag1.Equals(tag2);

        public static bool operator !=(Tags tag1, Tags tag2) => !tag1.Equals(tag2);

        public override readonly bool Equals(object obj)
        {
            return obj is Tags other && this.Equals(other);
        }

        public readonly bool Equals(Tags other)
        {
            var length = this.KeyValuePairs.Length;

            if (length != other.KeyValuePairs.Length)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                // Equality check for Keys
                if (!this.KeyValuePairs[i].Key.Equals(other.KeyValuePairs[i].Key, StringComparison.Ordinal))
                {
                    return false;
                }

                // Equality check for Values
                if (!this.KeyValuePairs[i].Value?.Equals(other.KeyValuePairs[i].Value) ?? other.KeyValuePairs[i].Value != null)
                {
                    return false;
                }
            }

            return true;
        }

        public override readonly int GetHashCode() => this.hashCode;
    }
}
