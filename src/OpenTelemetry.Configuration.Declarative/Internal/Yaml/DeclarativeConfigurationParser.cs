// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Walks a declarative-configuration YAML AST and parses it into the typed <see cref="DeclarativeConfiguration"/> model.
/// </summary>
/// <remarks>
/// This is the only place that depends on the YamlDotNet representation model and that applies the spec's
/// absent / present-null / present distinction (alongside environment-variable substitution performed by
/// <see cref="YamlNodeReader"/>).
/// </remarks>
internal static class DeclarativeConfigurationParser
{
    /// <summary>
    /// Builds the typed model from the (already validated) document root.
    /// </summary>
    /// <param name="root">The document root mapping <see cref="YamlMappingNode"/>.</param>
    /// <param name="fileFormat">The validated <c>file_format</c> value.</param>
    /// <returns>The typed <see cref="DeclarativeConfiguration"/>.</returns>
    internal static DeclarativeConfiguration Parse(YamlMappingNode root, string fileFormat) =>
        new(fileFormat)
        {
            Disabled = ReadDisabled(root),
            Resource = ReadResource(root),
        };

    private static ConfigProperty<bool> ReadDisabled(YamlMappingNode node) =>
        node.ReadBoolean(YamlKeys.Disabled);

    private static ConfigProperty<ResourceConfiguration> ReadResource(YamlMappingNode node) =>
        node.ReadMapping(YamlKeys.Resource, ReadResourceConfiguration);

    private static ResourceConfiguration ReadResourceConfiguration(YamlMappingNode node) =>
        new()
        {
            AttributesList = ReadAttributesList(node),
            Attributes = ReadAttributes(node),
        };

    private static ConfigProperty<string> ReadAttributesList(YamlMappingNode node) =>
        node.ReadString(YamlKeys.AttributesList);

    private static ConfigProperty<IReadOnlyList<ResourceAttributeEntry>> ReadAttributes(YamlMappingNode node)
    {
        var valueNode = node.GetValueNode(YamlKeys.Attributes);
        if (valueNode is null)
        {
            return ConfigProperty<IReadOnlyList<ResourceAttributeEntry>>.Absent;
        }

        if (valueNode is not YamlSequenceNode sequence)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.MalformedSection(
                YamlKeys.Attributes,
                $"Expected a sequence node but found {valueNode.NodeType}; no resource attributes will be emitted.");
            return ConfigProperty<IReadOnlyList<ResourceAttributeEntry>>.Absent;
        }

        var entries = new List<ResourceAttributeEntry>();
        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode attributeNode)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                    $"resource.attributes item is not a YAML mapping ({item.NodeType}) and will be skipped.");
                continue;
            }

            // Capture the entry faithfully; flat-format validation (name pattern, value type,
            // de-duplication, encoding) is applied by the bridge when projecting to OTEL_RESOURCE_ATTRIBUTES.
            entries.Add(new ResourceAttributeEntry(
                Name: attributeNode.GetScalarString(YamlKeys.Name),
                ScalarValue: ReadAttributeScalarValue(attributeNode, out var valueNodeKind),
                ValueNodeKind: valueNodeKind,
                RawType: attributeNode.GetScalarString(YamlKeys.Type)));
        }

        return ConfigProperty<IReadOnlyList<ResourceAttributeEntry>>.Create(entries);
    }

    private static string? ReadAttributeScalarValue(YamlMappingNode attributeNode, out AttributeValueNodeKind valueNodeKind)
    {
        var valueNode = attributeNode.GetValueNode(YamlKeys.Value);

        if (valueNode is null)
        {
            valueNodeKind = AttributeValueNodeKind.Absent;
            return null;
        }

        if (valueNode is YamlScalarNode scalar)
        {
            var scalarValue = scalar.GetScalarString();
            if (scalarValue is null)
            {
                // Key present but scalar resolved to YAML null (~ or 'null' keyword after substitution).
                // NullScalar, not Absent: the 'value' key exists in the document.
                valueNodeKind = AttributeValueNodeKind.NullScalar;
                return null;
            }

            valueNodeKind = AttributeValueNodeKind.Scalar;
            return scalarValue;
        }

        if (valueNode is YamlSequenceNode)
        {
            valueNodeKind = AttributeValueNodeKind.Sequence;
            return null;
        }

        // YamlMappingNode or any other node type.
        valueNodeKind = AttributeValueNodeKind.Mapping;
        return null;
    }
}
