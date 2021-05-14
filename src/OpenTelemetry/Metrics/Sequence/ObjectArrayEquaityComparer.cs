// <copyright file="ObjectArrayEquaityComparer.cs" company="OpenTelemetry Authors">
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
using System.Text;

namespace OpenTelemetry.Metrics
{
    public class ObjectArrayEquaityComparer : IEqualityComparer<object[]>
    {
        public bool Equals(object[] obj1, object[] obj2)
        {
            if (obj1.Length != obj2.Length)
            {
                return false;
            }

            for (int i = 0; i < obj1.Length; i++)
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
                foreach (var obj in objs)
                {
                    hash = (hash * 31) + obj.GetHashCode();
                }
            }

            return hash;
        }
    }
}
