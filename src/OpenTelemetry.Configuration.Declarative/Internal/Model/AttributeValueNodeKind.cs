// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The YAML node kind of the <c>value</c> field on an attribute entry.
/// </summary>
internal enum AttributeValueNodeKind
{
    /// <summary>
    /// The <c>value</c> key was absent from the mapping.
    /// </summary>
    Absent,

    /// <summary>
    /// The <c>value</c> key was present but its scalar resolved to YAML null (e.g. <c>value: ~</c>).
    /// </summary>
    NullScalar,

    /// <summary>
    /// The <c>value</c> key was present with a non-null scalar node.
    /// </summary>
    Scalar,

    /// <summary>
    /// The <c>value</c> key was present with a YAML sequence (array) node.
    /// </summary>
    Sequence,

    /// <summary>
    /// The <c>value</c> key was present with a YAML mapping (object) node.
    /// </summary>
    Mapping,
}
