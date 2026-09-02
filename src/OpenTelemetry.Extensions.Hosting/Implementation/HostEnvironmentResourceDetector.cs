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

    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>();

        if (!this.IsAttributeSetInConfiguration("service.name") &&
            !string.IsNullOrWhiteSpace(environment.ApplicationName))
        {
            attributes.Add(new("service.name", environment.ApplicationName));
        }

        if (!this.IsAttributeInOtelResourceAttributes("deployment.environment.name") &&
            !string.IsNullOrWhiteSpace(environment.EnvironmentName))
        {
            attributes.Add(new("deployment.environment.name", environment.EnvironmentName));
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

        if (attributeName == "service.name" && !string.IsNullOrWhiteSpace(configuration[OtelServiceNameKey]))
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
            var eq = pair.IndexOf('=');
#else
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
#endif
            if (eq > 0 && string.Equals(pair.Substring(0, eq).Trim(), attributeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
