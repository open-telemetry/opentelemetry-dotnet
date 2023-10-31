// <copyright file="RequestMethodHelper.cs" company="OpenTelemetry Authors">
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

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace OpenTelemetry.Internal;

internal static class RequestMethodHelper
{
#if NET8_0_OR_GREATER
    internal static readonly FrozenDictionary<string, string> KnownMethods;
#else
    internal static readonly Dictionary<string, string> KnownMethods;
#endif

    static RequestMethodHelper()
    {
        // KnownMethods ignores case. Use the value returned by the dictionary to have a consistent case.
#if NET8_0_OR_GREATER
        KnownMethods = FrozenDictionary.ToFrozenDictionary(
            new[]
            {
                KeyValuePair.Create("GET", "GET"),
                KeyValuePair.Create("PUT", "PUT"),
                KeyValuePair.Create("POST", "POST"),
                KeyValuePair.Create("DELETE", "DELETE"),
                KeyValuePair.Create("HEAD", "HEAD"),
                KeyValuePair.Create("OPTIONS", "OPTIONS"),
                KeyValuePair.Create("TRACE", "TRACE"),
                KeyValuePair.Create("PATCH", "PATCH"),
                KeyValuePair.Create("CONNECT", "CONNECT"),
            },
            StringComparer.OrdinalIgnoreCase);
#else
        KnownMethods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "GET", "GET" },
            { "PUT", "PUT" },
            { "POST", "POST" },
            { "DELETE", "DELETE" },
            { "HEAD", "HEAD" },
            { "OPTIONS", "OPTIONS" },
            { "TRACE", "TRACE" },
            { "PATCH", "PATCH" },
            { "CONNECT", "CONNECT" },
        };
#endif
    }
}
