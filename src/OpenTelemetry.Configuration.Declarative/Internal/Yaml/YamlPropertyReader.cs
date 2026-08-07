// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Extension methods that read typed values from a <see cref="YamlMappingNode"/> into <see cref="ConfigProperty{T}"/> results.
/// </summary>
internal static class YamlPropertyReader
{
    internal static ConfigProperty<bool> ReadBoolean(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ConfigProperty<bool>.Absent;
        }

        if (valueNode is not YamlScalarNode scalar)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.MalformedSection(
                key,
                $"Expected a scalar node but found {valueNode.NodeType}; the '{key}' setting will be ignored.");
            return ConfigProperty<bool>.Absent;
        }

        // A YAML null scalar (e.g. 'disabled: ~') is present-null, not invalid.
        var raw = scalar.GetScalarString();
        if (raw is null)
        {
            return ConfigProperty<bool>.Null;
        }

        raw = raw.Trim();

        // Present but resolving to empty (e.g. a quoted empty/whitespace scalar) is treated as present-null.
        if (raw.Length == 0)
        {
            return ConfigProperty<bool>.Null;
        }

        // bool.TryParse accepts only "true"/"false" (case-insensitive). YAML 1.1 aliases such as
        // "yes"/"no"/"on"/"off" are intentionally not handled: the spec requires YAML 1.2 core
        // schema, which recognises only "true" and "false" as boolean values.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/data-model.md#yaml-file-format
        if (bool.TryParse(raw, out var boolValue))
        {
            return ConfigProperty<bool>.Create(boolValue);
        }

        // The key is present but the value cannot be parsed as a boolean. Return Null (not Absent) because
        // the key was present in the document: the invalid value is logged and the field falls back to its
        // null behaviour (same as if it were absent), but Null is semantically more accurate than Absent.
        OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidBooleanValue(key, raw);
        return ConfigProperty<bool>.Null;
    }

    internal static ConfigProperty<string> ReadString(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ConfigProperty<string>.Absent;
        }

        if (valueNode is not YamlScalarNode scalar)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.MalformedSection(
                key,
                $"Expected a scalar node but found {valueNode.NodeType}; the '{key}' setting will be ignored.");
            return ConfigProperty<string>.Absent;
        }

        var value = scalar.GetScalarString();
        if (value is null)
        {
            return ConfigProperty<string>.Null;
        }

        // Present but resolving to empty or whitespace-only is treated as present-null, consistent with ReadBoolean.
        value = value.Trim();
        return value.Length == 0 ? ConfigProperty<string>.Null : ConfigProperty<string>.Create(value);
    }

    internal static ConfigProperty<T> ReadMapping<T>(this YamlMappingNode node, string key, Func<YamlMappingNode, T> factory)
    {
        var valueNode = node.GetValueNode(key);

        if (valueNode is null)
        {
            return ConfigProperty<T>.Absent;
        }

        if (valueNode is YamlMappingNode mapping)
        {
            return ConfigProperty<T>.Create(factory(mapping));
        }

        // A YAML null scalar (e.g. 'resource: ~') is present-null, not malformed.
        if (valueNode is YamlScalarNode scalar && scalar.GetScalarString() is null)
        {
            return ConfigProperty<T>.Null;
        }

        OpenTelemetryDeclarativeConfigurationEventSource.Log.MalformedSection(
            key,
            $"Expected a mapping node but found {valueNode.NodeType}; the '{key}' section will be ignored.");
        return ConfigProperty<T>.Absent;
    }
}
