// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources;

internal sealed class OtelServiceNameEnvVarDetector : IResourceDetector
{
    public const string EnvVarKey = "OTEL_SERVICE_NAME";

    private readonly IConfiguration configuration;

    public OtelServiceNameEnvVarDetector(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Resource Detect()
    {
        var resource = Resource.Empty;

        if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
        {
            resource = new Resource(new Dictionary<string, object>
            {
                [ResourceSemanticConventions.AttributeServiceName] = envResourceAttributeValue,
            });
        }

        return resource;
    }
}
