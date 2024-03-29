// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal;

internal static class RequestMethodHelper
{
    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#common-attributes
    public const string OtherHttpMethod = "_OTHER";

#if NET8_0_OR_GREATER
    internal static readonly FrozenDictionary<string, string> KnownMethods;
#else
    internal static readonly Dictionary<string, string> KnownMethods;
#endif

    static RequestMethodHelper()
    {
        var knownMethodSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

        // KnownMethods ignores case. Use the value returned by the dictionary to have a consistent case.
#if NET8_0_OR_GREATER
        KnownMethods = FrozenDictionary.ToFrozenDictionary(knownMethodSet, StringComparer.OrdinalIgnoreCase);
#else
        KnownMethods = knownMethodSet;
#endif
    }

    public static string GetNormalizedHttpMethod(string method)
    {
        return KnownMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }

    public static void SetHttpMethodTag(Activity activity, string originalHttpMethod)
    {
        var normalizedHttpMethod = GetNormalizedHttpMethod(originalHttpMethod);
        activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, normalizedHttpMethod);

        if (originalHttpMethod != normalizedHttpMethod)
        {
            activity.SetTag(SemanticConventions.AttributeHttpRequestMethodOriginal, originalHttpMethod);
        }
    }

    public static void SetActivityDisplayName(Activity activity, string method, string? httpRoute = null)
    {
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md#name

        var namePrefix = KnownMethods.TryGetValue(method, out var httpMethod) ? httpMethod : "HTTP";
        activity.DisplayName = string.IsNullOrEmpty(httpRoute) ? namePrefix : $"{namePrefix} {httpRoute}";
    }
}
