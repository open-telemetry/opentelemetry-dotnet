// <copyright file="DefaultLogStateConverter.cs" company="OpenTelemetry Authors">
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

#if NET461_OR_GREATER || NETSTANDARD2_0 || NET5_0_OR_GREATER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Logs
{
    internal static class DefaultLogStateConverter
    {
        public static void ConvertState(ActivityTagsCollection tags, IReadOnlyList<KeyValuePair<string, object>> state)
        {
            for (int i = 0; i < state.Count; i++)
            {
                KeyValuePair<string, object> item = state[i];

                if (!string.IsNullOrEmpty(item.Key))
                {
                    ConvertState(tags, $"state.{item.Key}", item.Value);
                }
                else
                {
                    ConvertState(tags, $"state", item.Value);
                }
            }
        }

        public static void ConvertScope(ActivityTagsCollection tags, int index, object scope)
        {
            ConvertState(tags, $"scope[{index}]", scope);
        }

        private static void ConvertState(ActivityTagsCollection tags, string keyPrefix, object state)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object>> stateList)
            {
                for (int i = 0; i < stateList.Count; i++)
                {
                    KeyValuePair<string, object> item = stateList[i];

                    ConvertState(tags, $"{keyPrefix}.{item.Key}", item.Value);
                }
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> stateValues)
            {
                foreach (KeyValuePair<string, object> item in stateValues)
                {
                    ConvertState(tags, $"{keyPrefix}.{item.Key}", item.Value);
                }
            }
            else if (state != null)
            {
                Type type = state.GetType();
                if (type.IsValueType || type == typeof(string))
                {
                    if (keyPrefix == "state.{OriginalFormat}")
                    {
                        keyPrefix = "Format";
                    }

                    tags[keyPrefix] = state;
                }
                else if (state is IEnumerable enumerable)
                {
                    int index = 0;
                    foreach (object stateItem in enumerable)
                    {
                        ConvertState(tags, $"{keyPrefix}[{index++}]", stateItem);
                    }
                }
                else
                {
                    tags[keyPrefix] = state.ToString();
                }
            }
        }
    }
}
#endif
