// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A single <c>resource.attributes</c> entry as authored, before flat-format validation.
/// </summary>
/// <param name="Name">The required, substituted attribute name.</param>
/// <param name="ScalarValue">
/// The substituted scalar value. Only non-null when <see cref="ValueNodeKind"/> is
/// <see cref="AttributeValueNodeKind.Scalar"/>; null for YAML-null and sequence values.
/// </param>
/// <param name="SequenceValues">
/// The substituted and resolved sequence values. Only non-null when <see cref="ValueNodeKind"/> is
/// <see cref="AttributeValueNodeKind.Sequence"/>.
/// </param>
/// <param name="ValueNodeKind">The YAML node kind of the <c>value</c> field.</param>
/// <param name="ScalarKind">The resolved scalar kind, or <see langword="null"/> for non-scalar values.</param>
/// <param name="Type">The validated declarative attribute type.</param>
internal sealed record ResourceAttributeEntry(
    string Name,
    string? ScalarValue,
    IReadOnlyList<ResolvedYamlScalar>? SequenceValues,
    AttributeValueNodeKind ValueNodeKind,
    YamlScalarKind? ScalarKind,
    ResourceAttributeType Type)
{
    /// <summary>
    /// Returns the scalar value when this entry carries a non-null scalar.
    /// Use this instead of reading <see cref="ScalarValue"/> directly: <see cref="ScalarValue"/> is also
    /// null for YAML-null and sequence values, so a raw null check silently skips those cases
    /// without ever reaching their diagnostic paths.
    /// </summary>
    /// <param name="value">When this method returns <see langword="true"/>, the non-null scalar value.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="ValueNodeKind"/> is <see cref="AttributeValueNodeKind.Scalar"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    [MemberNotNullWhen(true, nameof(ScalarValue))]
    internal bool TryGetScalarValue([NotNullWhen(true)] out string? value)
    {
        value = this.ValueNodeKind == AttributeValueNodeKind.Scalar ? this.ScalarValue : null;
        return value is not null;
    }

    /// <summary>
    /// Returns the sequence values when this entry carries an array value.
    /// </summary>
    /// <param name="values">When this method returns <see langword="true"/>, the sequence values.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="ValueNodeKind"/> is <see cref="AttributeValueNodeKind.Sequence"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    [MemberNotNullWhen(true, nameof(SequenceValues))]
    internal bool TryGetSequenceValues([NotNullWhen(true)] out IReadOnlyList<ResolvedYamlScalar>? values)
    {
        values = this.ValueNodeKind == AttributeValueNodeKind.Sequence ? this.SequenceValues : null;
        return values is not null;
    }
}
