// <copyright file="NameAndKeysEqualityComparer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    internal class NameAndKeysEqualityComparer : IEqualityComparer<AggregatorStore.NameAndKeys>
    {
        public bool Equals(AggregatorStore.NameAndKeys item1, AggregatorStore.NameAndKeys item2)
        {
            if (ReferenceEquals(item1, item2))
            {
                return true;
            }

            if (ReferenceEquals(item1, null) || ReferenceEquals(item2, null))
            {
                return false;
            }

            if (!item1.Name.Equals(item2.Name, StringComparison.Ordinal))
            {
                return false;
            }

            var len1 = item1.Keys.Length;

            if (len1 != item2.Keys.Length)
            {
                return false;
            }

            for (int i = 0; i < len1; i++)
            {
                if (!item1.Keys[i].Equals(item2.Keys[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(AggregatorStore.NameAndKeys item)
        {
            int hash = 17;

            unchecked
            {
                hash = (hash * 31) + item.Name.GetHashCode();

                for (int i = 0; i < item.Keys.Length; i++)
                {
                    hash = (hash * 31) + item.Keys[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
