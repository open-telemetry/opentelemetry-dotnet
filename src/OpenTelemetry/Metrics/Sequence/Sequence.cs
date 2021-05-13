// <copyright file="Sequence.cs" company="OpenTelemetry Authors">
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
    public struct Sequence<T> : ISequence, IEquatable<Sequence<T>>
    {
        internal readonly T[] Values;

        public Sequence(params T[] values)
        {
            if (typeof(T) == typeof(string) || typeof(T) == typeof(object))
            {
                this.Values = values;
            }
            else
            {
                throw new TypeInitializationException(nameof(T), null);
            }
        }

        public override int GetHashCode()
        {
            // // NET50 allows...
            // var hash = new HashCode();
            // for (int i = 0; i < this.values.Length; i++)
            // {
            //     hash.Add(this.values[i]);
            // }
            // return hash.ToHashCode();

            int hash = 17;

            unchecked
            {
                for (int i = 0; i < this.Values.Length; i++)
                {
                    hash = (hash * 31) + this.Values[i].GetHashCode();
                }
            }

            return hash;
        }

        public bool Equals(Sequence<T> other)
        {
            if (this.Values.Length != other.Values.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Values.Length; i++)
            {
                var value = this.Values[i];
                var value2 = other.Values[i];

                if (value is string str1 && value2 is string str2)
                {
                    if (str1 != str2)
                    {
                        return false;
                    }
                }
                else if (value is object obj1 && value2 is object obj2)
                {
                    if (obj1 != obj2)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is Sequence<T> seq && this.Equals(seq);
        }
    }
}
