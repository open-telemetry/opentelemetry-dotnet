// <copyright file="ObjectArrayEqualityComparer.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    internal class ObjectArrayEqualityComparer : IEqualityComparer<object[]>
    {
        public bool Equals(object[] obj1, object[] obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
            {
                return false;
            }

            var len1 = obj1.Length;

            if (len1 != obj2.Length)
            {
                return false;
            }

            for (int i = 0; i < len1; i++)
            {
                if (obj1[i] != obj2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object[] objs)
        {
            int hash = 17;

            unchecked
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    hash = (hash * 31) + objs[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
