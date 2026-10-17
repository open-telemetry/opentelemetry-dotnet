// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A node of the configuration schema graph.
/// </summary>
internal sealed class SchemaNode
{
    /// <summary>
    /// Gets or sets the types the node admits.
    /// </summary>
    internal SchemaValueTypes ValueTypes { get; set; }

    /// <summary>
    /// Gets or sets the properties the node defines.
    /// </summary>
    internal IReadOnlyDictionary<string, SchemaNode> Properties { get; set; } = new Dictionary<string, SchemaNode>(0);

    /// <summary>
    /// Gets or sets how the node treats properties it does not define.
    /// </summary>
    internal AdditionalPropertiesKind AdditionalProperties { get; set; }

    /// <summary>
    /// Gets or sets the schema for additional properties when <see cref="AdditionalProperties"/> is
    /// <see cref="AdditionalPropertiesKind.Schema"/>.
    /// </summary>
    internal SchemaNode? AdditionalPropertiesSchema { get; set; }

    /// <summary>
    /// Gets or sets the schema for sequence items.
    /// </summary>
    internal SchemaNode? Items { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the node is described by alternatives.
    /// </summary>
    internal bool HasOneOf { get; set; }
}
