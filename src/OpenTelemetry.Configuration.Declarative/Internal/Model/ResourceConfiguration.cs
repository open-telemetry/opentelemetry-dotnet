// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Typed model for the declarative-configuration <c>resource</c> section (the subset this package
/// currently supports: <c>attributes</c> and <c>attributes_list</c>).
/// </summary>
/// <remarks>
/// A source-agnostic data record.
/// </remarks>
internal sealed record ResourceConfiguration
{
    /// <summary>
    /// Gets the pre-encoded <c>attributes_list</c> string (OTEL_RESOURCE_ATTRIBUTES format).
    /// </summary>
    public ConfigProperty<string> AttributesList { get; init; }

    /// <summary>
    /// Gets the structured <c>attributes</c> entries, in document order.
    /// </summary>
    public ConfigProperty<IReadOnlyList<ResourceAttributeEntry>> Attributes { get; init; }
}
