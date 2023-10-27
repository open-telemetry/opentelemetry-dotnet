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

using Microsoft.AspNetCore.Http;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
using System.Collections.Generic;
#endif

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes;

#if NET8_0_OR_GREATER
    internal static readonly FrozenDictionary<string, string> KnownMethods = FrozenDictionary.ToFrozenDictionary(
        new[]
        {
            KeyValuePair.Create(HttpMethods.Connect, HttpMethods.Connect),
            KeyValuePair.Create(HttpMethods.Delete, HttpMethods.Delete),
            KeyValuePair.Create(HttpMethods.Get, HttpMethods.Get),
            KeyValuePair.Create(HttpMethods.Head, HttpMethods.Head),
            KeyValuePair.Create(HttpMethods.Options, HttpMethods.Options),
            KeyValuePair.Create(HttpMethods.Patch, HttpMethods.Patch),
            KeyValuePair.Create(HttpMethods.Post, HttpMethods.Post),
            KeyValuePair.Create(HttpMethods.Put, HttpMethods.Put),
            KeyValuePair.Create(HttpMethods.Trace, HttpMethods.Trace)
        },
        StringComparer.OrdinalIgnoreCase);
#else
    internal static readonly Dictionary<string, string> KnownMethods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { HttpMethods.Connect, HttpMethods.Connect },
        { HttpMethods.Delete, HttpMethods.Delete },
        { HttpMethods.Get, HttpMethods.Get },
        { HttpMethods.Head, HttpMethods.Head },
        { HttpMethods.Options, HttpMethods.Options },
        { HttpMethods.Patch, HttpMethods.Patch },
        { HttpMethods.Post, HttpMethods.Post },
        { HttpMethods.Put, HttpMethods.Put },
        { HttpMethods.Trace, HttpMethods.Trace },
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

    public static object GetBoxedStatusCode(int statusCode)
    {
        if (statusCode >= 100 && statusCode < 600)
        {
            return BoxedStatusCodes[statusCode - 100];
        }

        return statusCode;
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
