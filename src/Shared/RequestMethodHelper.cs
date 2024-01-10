// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal;

internal sealed class RequestMethodHelper
{
    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#common-attributes
    public const string OtherHttpMethod = "_OTHER";

    private static readonly List<string> DefaultKnownMethods = new()
        {
            { "GET" },
            { "PUT" },
            { "POST" },
            { "DELETE" },
            { "HEAD" },
            { "OPTIONS" },
            { "TRACE" },
            { "PATCH" },
            { "CONNECT" },
        };

    public RequestMethodHelper(IConfiguration configuration)
    {
        Debug.Assert(configuration != null, "configuration was null");

        if (configuration!.TryGetStringValue("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", out var knownHttpMethods))
        {
            var splitArray = knownHttpMethods!.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            this.KnownMethods = GetKnownMethods(splitArray);
        }
        else
        {
            this.KnownMethods = GetKnownMethods(DefaultKnownMethods);
        }
    }

#if NET8_0_OR_GREATER
    public FrozenDictionary<string, string> KnownMethods { get; private set; }
#else
    public Dictionary<string, string> KnownMethods { get; private set; }
#endif

    public static void RegisterServices(IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        services!.TryAddSingleton<RequestMethodHelper>();
    }

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
        Debug.Assert(configuredKnownMethods != null, "configuredKnownMethods was null");

        var knownMethods = configuredKnownMethods.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);

#if NET8_0_OR_GREATER
        return FrozenDictionary.ToFrozenDictionary(knownMethods, StringComparer.OrdinalIgnoreCase);
#else
        return knownMethods.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
#endif
    }
}
