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

#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<string, string> knownMethods;
#else
    private Dictionary<string, string> knownMethods;
#endif

    public RequestMethodHelper(string configuredKnownMethods)
    {
        var splitArray = configuredKnownMethods.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

        this.knownMethods = GetKnownMethods(splitArray);
    }

    public RequestMethodHelper(List<string> configuredKnownMethods)
    {
        this.knownMethods = GetKnownMethods(configuredKnownMethods);
    }

#if NET8_0_OR_GREATER
    public string GetNormalizedHttpMethod(string method)
#else
    public string GetNormalizedHttpMethod(string method, Dictionary<string, string> knownMethods = null)
#endif
    {
        return this.knownMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }

#if NET8_0_OR_GREATER
    public void SetHttpMethodTag(Activity activity, string method)
#else
    public void SetHttpMethodTag(Activity activity, string method)
#endif
    {
        if (this.knownMethods.TryGetValue(method, out var normalizedMethod))
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
    public void SetHttpClientActivityDisplayName(Activity activity, string method, Dictionary<string, string> knownMethods)
#endif
    {
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#name
        activity.DisplayName = this.knownMethods.TryGetValue(method, out var httpMethod) ? httpMethod : "HTTP";
    }

#if NET8_0_OR_GREATER
    private static FrozenDictionary<string, string> GetKnownMethods(string configuredKnownMethods)
#else
    private static Dictionary<string, string> GetKnownMethods(string configuredKnownMethods)
#endif
    {
        var splitArray = configuredKnownMethods.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

        return GetKnownMethods(splitArray);
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
                knownMethods = DefaultKnownMethods.Where(x => configuredKnownMethods.Contains(x.Key, StringComparer.InvariantCultureIgnoreCase));
            }
        }

#if NET8_0_OR_GREATER
        return FrozenDictionary.ToFrozenDictionary(knownMethods, StringComparer.OrdinalIgnoreCase);
#else
        return knownMethods.ToDictionary(x => x.Key, x => x.Value);
#endif
    }
}
