// <copyright file="NameAndTagKeys.cs" company="OpenTelemetry Authors">
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
    internal class NameAndTagKeys : IEquatable<NameAndTagKeys>
    {
        internal NameAndTagKeys()
            : this(null, null)
        {
        }

        internal NameAndTagKeys(string name, string[] keys)
        {
            this.Name = name;
            this.Keys = keys;
        }

        internal string Name { get; set; }

        internal string[] Keys { get; set; }

        public static bool operator ==(NameAndTagKeys lhs, NameAndTagKeys rhs)
        {
            if (lhs == null)
            {
                if (rhs == null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        public static bool operator !=(NameAndTagKeys lhs, NameAndTagKeys rhs) => !(lhs == rhs);

        public override bool Equals(object obj) => this.Equals(obj as NameAndTagKeys);

        public bool Equals(NameAndTagKeys other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!this.Name.Equals(other.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (object.ReferenceEquals(this.Keys, other.Keys))
            {
                return true;
            }

            if (this.Keys is null || other.Keys is null)
            {
                return false;
            }

            var len = this.Keys.Length;

            if (len != other.Keys.Length)
            {
                return false;
            }

            for (int i = 0; i < len; i++)
            {
                if (!this.Keys[i].Equals(other.Keys[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 17;

            unchecked
            {
                hash = (hash * 31) + this.Name.GetHashCode();

                if (this.Keys != null)
                {
                    for (int i = 0; i < this.Keys.Length; i++)
                    {
                        hash = (hash * 31) + this.Keys[i].GetHashCode();
                    }
                }
            }

            return hash;
        }
    }
}
