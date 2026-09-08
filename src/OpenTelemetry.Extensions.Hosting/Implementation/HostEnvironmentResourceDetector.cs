// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.Hosting.Implementation;

// Contributes service.name and deployment.environment.name from the host environment as
// low-priority defaults. Runs after the SDK's OtelEnvResourceDetector and
// OtelServiceNameEnvVarDetector in the ResourceBuilder pipeline, so it actively skips keys
// that those detectors would supply to preserve their higher-priority values. Application
// ConfigureResource callbacks run after this detector and win.
internal sealed class HostEnvironmentResourceDetector(
    IHostEnvironment environment,
    IConfiguration? configuration) : IResourceDetector
{
    private const string OtelServiceNameKey = "OTEL_SERVICE_NAME";
    private const string OtelResourceAttributesKey = "OTEL_RESOURCE_ATTRIBUTES";
    private const string ServiceNameAttribute = "service.name";
    private const string DeploymentEnvironmentNameAttribute = "deployment.environment.name";

    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>();

        if (!this.IsAttributeSetInConfiguration(ServiceNameAttribute) &&
            !string.IsNullOrWhiteSpace(environment.ApplicationName))
        {
            attributes.Add(new(ServiceNameAttribute, environment.ApplicationName));
        }

        if (!this.IsAttributeInOtelResourceAttributes(DeploymentEnvironmentNameAttribute) &&
            !string.IsNullOrWhiteSpace(environment.EnvironmentName))
        {
            attributes.Add(new(DeploymentEnvironmentNameAttribute, NormalizeEnvironmentName(environment.EnvironmentName)));
        }

        return new Resource(attributes);
    }

    internal bool IsAttributeSetInConfiguration(string attributeName)
    {
        if (configuration == null)
        {
            // IConfiguration is optional; without it no env-var suppression is possible.
            return false;
        }

        if (attributeName == ServiceNameAttribute && !string.IsNullOrWhiteSpace(configuration[OtelServiceNameKey]))
        {
            return true;
        }

        return this.IsAttributeInOtelResourceAttributes(attributeName);
    }

    internal bool IsAttributeInOtelResourceAttributes(string attributeName)
    {
        var raw = configuration?[OtelResourceAttributesKey];
        if (raw == null)
        {
            return false;
        }

        foreach (var pair in raw.Split(','))
        {
#if NETFRAMEWORK || NETSTANDARD2_0
            var index = pair.IndexOf('=');
#else
            var index = pair.IndexOf('=', StringComparison.Ordinal);
#endif
            if (index > 0 && string.Equals(pair.Substring(0, index).Trim(), attributeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The spec (https://opentelemetry.io/docs/specs/semconv/registry/attributes/deployment/#deployment-attributes) mandates these four well-known values in lowercase.
    private static string NormalizeEnvironmentName(string name) => name switch
    {
        _ when string.Equals(name, "development", StringComparison.OrdinalIgnoreCase) => "development",
        _ when string.Equals(name, "production", StringComparison.OrdinalIgnoreCase) => "production",
        _ when string.Equals(name, "staging", StringComparison.OrdinalIgnoreCase) => "staging",
        _ when string.Equals(name, "test", StringComparison.OrdinalIgnoreCase) => "test",
        _ => name,
    };
}
