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
                KeyValuePair<string, object> stateItem = state[i];

                object value = stateItem.Value;
                if (value != null)
                {
                    if (string.IsNullOrEmpty(stateItem.Key))
                    {
                        tags["state"] = value;
                    }
                    else
                    {
                        tags[$"state.{stateItem.Key}"] = value;
                    }
                }
            }
        }

        public static void ConvertScope(ActivityTagsCollection tags, int index, LogRecordScope scope)
        {
            string prefix = $"scope[{index}]";

            foreach (KeyValuePair<string, object> scopeItem in scope)
            {
                object value = scopeItem.Value;
                if (value != null)
                {
                    if (string.IsNullOrEmpty(scopeItem.Key))
                    {
                        tags[prefix] = value;
                    }
                    else
                    {
                        tags[$"{prefix}.{scopeItem.Key}"] = value;
                    }
                }
            }
        }
    }
}
#endif
