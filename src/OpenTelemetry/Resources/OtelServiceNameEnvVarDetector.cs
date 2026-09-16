// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

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

        if (this.configuration.TryGetStringValue(EnvVarKey, out var envResourceAttributeValue))
        {
            resource = new(
                [new(ResourceSemanticConventions.AttributeServiceName, envResourceAttributeValue)],
                SchemaUrls.Get(SemanticConventionsVersion.Current));
        }

        return resource;
    }
}
