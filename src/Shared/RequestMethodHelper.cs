// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal;

internal class RequestMethodHelper
{
    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#common-attributes
    public const string OtherHttpMethod = "_OTHER";

    private static readonly Dictionary<string, string> DefaultKnownMethods = new(StringComparer.OrdinalIgnoreCase)
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

    public RequestMethodHelper(string configuredKnownMethods)
    {
        var splitArray = configuredKnownMethods.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

        this.KnownMethods = GetKnownMethods(splitArray);
    }

    public RequestMethodHelper(List<string> configuredKnownMethods)
    {
        this.KnownMethods = GetKnownMethods(configuredKnownMethods);
    }

#if NET8_0_OR_GREATER
    public FrozenDictionary<string, string> KnownMethods { get; private set; }
#else
    public Dictionary<string, string> KnownMethods { get; private set; }
#endif

#if NET8_0_OR_GREATER
    public string GetNormalizedHttpMethod(string method)
#else
    public string GetNormalizedHttpMethod(string method)
#endif
    {
        return this.KnownMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }

#if NET8_0_OR_GREATER
    public void SetHttpMethodTag(Activity activity, string method)
#else
    public void SetHttpMethodTag(Activity activity, string method)
#endif
    {
        if (this.KnownMethods.TryGetValue(method, out var normalizedMethod))
        {
            activity?.SetTag(SemanticConventions.AttributeHttpRequestMethod, normalizedMethod);
        }
        else
        {
            activity?.SetTag(SemanticConventions.AttributeHttpRequestMethod, OtherHttpMethod);
            activity?.SetTag(SemanticConventions.AttributeHttpRequestMethodOriginal, method);
        }
    }

#if NET8_0_OR_GREATER
    public void SetHttpClientActivityDisplayName(Activity activity, string method)
#else
    public void SetHttpClientActivityDisplayName(Activity activity, string method)
#endif
    {
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#name
        activity.DisplayName = this.KnownMethods.TryGetValue(method, out var httpMethod) ? httpMethod : "HTTP";
    }

#if NET8_0_OR_GREATER
    private static FrozenDictionary<string, string> GetKnownMethods(List<string> configuredKnownMethods)
#else
    private static Dictionary<string, string> GetKnownMethods(List<string> configuredKnownMethods)
#endif
    {
        IEnumerable<KeyValuePair<string, string>> knownMethods = DefaultKnownMethods;

        if (configuredKnownMethods != null)
        {
            if (configuredKnownMethods.Count > 0)
            {
                knownMethods = DefaultKnownMethods.Where(x => configuredKnownMethods.Contains(x.Key, StringComparer.OrdinalIgnoreCase));
            }
        }

#if NET8_0_OR_GREATER
        return FrozenDictionary.ToFrozenDictionary(knownMethods, StringComparer.OrdinalIgnoreCase);
#else
        return knownMethods.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
#endif
    }
}
