// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

internal sealed class OtelEnvResourceDetector : IResourceDetector
{
    public const string EnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";

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

        if (PercentEncodingHelper.TryExtractBaggage([resourceAttributes], out var baggage) && baggage != null)
        {
            foreach (var kvp in baggage)
            {
                attributes.Add(new KeyValuePair<string, object>(kvp.Key, kvp.Value));
            }
        }

        return attributes;
    }
}
