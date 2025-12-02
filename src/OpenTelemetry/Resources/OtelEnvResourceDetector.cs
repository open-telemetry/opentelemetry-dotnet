// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources;

internal sealed class OtelEnvResourceDetector : IResourceDetector
{
    public const string EnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";
    private const char AttributeListSplitter = ',';
    private static readonly char[] AttributeKeyValueSplitter = ['='];

    private readonly IConfiguration configuration;

    public OtelEnvResourceDetector(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Resource Detect()
    {
        var resource = Resource.Empty;

        if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
        {
            var attributes = ParseResourceAttributes(envResourceAttributeValue);
            resource = new Resource(attributes);
        }

        return resource;
    }

    private static List<KeyValuePair<string, object>> ParseResourceAttributes(string resourceAttributes)
    {
        var attributes = new List<KeyValuePair<string, object>>();

        string[] rawAttributes = resourceAttributes.Split(AttributeListSplitter);
        foreach (string rawKeyValuePair in rawAttributes)
        {
            string[] keyValuePair = rawKeyValuePair.Split(AttributeKeyValueSplitter, 2);
            if (keyValuePair.Length != 2)
            {
                continue;
            }

            var value = WebUtility.UrlDecode(keyValuePair[1].Trim());
            attributes.Add(new KeyValuePair<string, object>(keyValuePair[0].Trim(), value));
        }

        return attributes;
    }
}
