// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Typed model for the declarative-configuration <c>resource</c> section.
/// </summary>
/// <remarks>
/// A source-agnostic data record.
/// </remarks>
internal sealed record ResourceConfiguration
{
    /// <summary>
    /// Gets the pre-encoded <c>attributes_list</c> string (<c>OTEL_RESOURCE_ATTRIBUTES</c> format).
    /// </summary>
    public ModelProperty<string> AttributesList { get; init; }

    /// <summary>
    /// Gets the structured <c>attributes</c> entries, in document order.
    /// </summary>
    public ModelProperty<IReadOnlyList<ResourceAttributeEntry>> Attributes { get; init; }

    /// <summary>
    /// Gets the schema URL from the <c>schema_url</c> field.
    /// </summary>
    public ModelProperty<string> SchemaUrl { get; init; }
}
