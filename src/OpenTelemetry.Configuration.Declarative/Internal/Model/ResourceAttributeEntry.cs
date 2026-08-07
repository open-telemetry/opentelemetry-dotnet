// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A single <c>resource.attributes</c> entry as authored, before flat-format validation.
/// </summary>
/// <param name="Name">The substituted attribute name, or <see langword="null"/> if the name is absent or non-scalar.</param>
/// <param name="ScalarValue">
/// The substituted scalar value. Only non-null when <see cref="ValueNodeKind"/> is
/// <see cref="AttributeValueNodeKind.Scalar"/>; null for absent, YAML-null, sequence, and mapping values.
/// </param>
/// <param name="ValueNodeKind">The YAML node kind of the <c>value</c> field.</param>
/// <param name="RawType">
/// The raw value of the <c>type</c> field (e.g. <c>"string"</c>, <c>"string_array"</c>),
/// or <see langword="null"/> if the field is absent.
/// </param>
internal sealed record ResourceAttributeEntry(
    string? Name,
    string? ScalarValue,
    AttributeValueNodeKind ValueNodeKind,
    string? RawType)
{
    /// <summary>
    /// Returns the scalar value when this entry carries a non-null scalar.
    /// Use this instead of reading <see cref="ScalarValue"/> directly: <see cref="ScalarValue"/> is also
    /// null for absent, YAML-null, sequence, and mapping values, so a raw null check silently skips
    /// those cases without ever reaching any diagnostic path.
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
}
