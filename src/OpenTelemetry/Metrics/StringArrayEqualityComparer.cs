// <copyright file="StringArrayEqualityComparer.cs" company="OpenTelemetry Authors">
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
    internal sealed class StringArrayEqualityComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[] strings1, string[] strings2)
        {
            if (ReferenceEquals(strings1, strings2))
            {
                return true;
            }

            if (ReferenceEquals(strings1, null) || ReferenceEquals(strings2, null))
            {
                return false;
            }

            var len1 = strings1.Length;

            if (len1 != strings2.Length)
            {
                return false;
            }

            for (int i = 0; i < len1; i++)
            {
                if (!strings1[i].Equals(strings2[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(string[] strings)
        {
            int hash = 17;

            unchecked
            {
                for (int i = 0; i < strings.Length; i++)
                {
                    hash = (hash * 31) + strings[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
