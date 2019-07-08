// <copyright file="Collections.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Abstractions.Utils
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal static class Collections
    {
        public static string ToString<TKey, TValue>(IDictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            foreach (var kvp in dict)
            {
                sb.Append(kvp.Key.ToString());
                sb.Append("=");
                sb.Append(kvp.Value.ToString());
                sb.Append(" ");
            }

            return sb.ToString();
        }

        public static string ToString<T>(IEnumerable<T> list)
        {
            if (list == null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            foreach (var val in list)
            {
                if (val != null)
                {
                    sb.Append(val.ToString());
                    sb.Append(" ");
                }
            }

            return sb.ToString();
        }
    }
}
