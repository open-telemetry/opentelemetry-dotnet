// <copyright file="TelemetryHelper.cs" company="OpenTelemetry Authors">
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

using System.Net;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes;

#if NET8_0_OR_GREATER
    internal static readonly FrozenDictionary<string, string> KnownMethods = FrozenDictionary.ToFrozenDictionary(
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
    internal static readonly Dictionary<string, string> KnownMethods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

    static TelemetryHelper()
    {
        BoxedStatusCodes = new object[500];
        for (int i = 0, c = 100; i < BoxedStatusCodes.Length; i++, c++)
        {
            BoxedStatusCodes[i] = c;
        }
    }

    public static object GetBoxedStatusCode(HttpStatusCode statusCode)
    {
        int intStatusCode = (int)statusCode;
        if (intStatusCode >= 100 && intStatusCode < 600)
        {
            return BoxedStatusCodes[intStatusCode - 100];
        }

        return intStatusCode;
    }

    public static bool TryResolveHttpMethod(string method, out string resolvedMethod)
    {
        if (KnownMethods.TryGetValue(method, out var result))
        {
            // KnownMethods ignores case. Use the value returned by the dictionary to have a consistent case.
            resolvedMethod = result;
            return true;
        }

        // Set to default "_OTHER" as per spec.
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#common-attributes
        resolvedMethod = "_OTHER";
        return false;
    }
}
