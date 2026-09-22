// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Converts a typed <see cref="DeclarativeConfiguration"/> model into the flat, env-var-style
/// key/value pairs consumed by the OpenTelemetry SDK's IConfiguration readers.
/// </summary>
/// <remarks>
/// This is a lossy, one-way conversion: only the fields expressible in the env-var format are
/// emitted. Fields absent or present-null in the model produce no output, leaving SDK defaults
/// and other IConfiguration sources in effect.
/// </remarks>
internal static class DeclarativeConfigurationConverter
{
    internal const string DisabledKey = OtelEnvironmentVariables.SdkDisabled;
    internal const string ResourceAttributesKey = OtelEnvironmentVariables.ResourceAttributes;

    /// <summary>
    /// Converts <paramref name="config"/> into <paramref name="data"/> as flat OTel configuration keys.
    /// </summary>
    /// <param name="config">The typed configuration model to convert.</param>
    /// <param name="data">Dictionary to populate with flat key/value pairs.</param>
    internal static void Convert(DeclarativeConfiguration config, IDictionary<string, string?> data)
    {
        EmitDisabled(config.Disabled, data);
        EmitResource(config.Resource, data);
    }

    private static void EmitDisabled(ModelProperty<bool> disabled, IDictionary<string, string?> data)
    {
        if (disabled.TryGetValue(out var value))
        {
            data[DisabledKey] = value ? "true" : "false";
        }
    }

    private static void EmitResource(ModelProperty<ResourceConfiguration> resource, IDictionary<string, string?> data)
    {
        if (resource.TryGetValue(out var resourceConfig))
        {
            // rawAttributesList holds the pre-encoded attributes_list, comma-separated string.
            if (resourceConfig.AttributesList.TryGetValue(out var rawAttributesList))
            {
                var trimmed = rawAttributesList.Trim();
                if (trimmed.Length > 0)
                {
                    data[ResourceAttributesKey] = trimmed;
                }
            }
        }
    }
}
