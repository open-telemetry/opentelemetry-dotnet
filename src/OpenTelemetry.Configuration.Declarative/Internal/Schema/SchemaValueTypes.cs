// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The JSON types a schema node admits.
/// </summary>
[Flags]
internal enum SchemaValueTypes
{
    /// <summary>
    /// The node does not constrain the type.
    /// </summary>
    None = 0,

    /// <summary>
    /// A mapping.
    /// </summary>
    Object = 1,

    /// <summary>
    /// A sequence.
    /// </summary>
    Array = 1 << 1,

    /// <summary>
    /// A string.
    /// </summary>
    String = 1 << 2,

    /// <summary>
    /// An integer.
    /// </summary>
    Integer = 1 << 3,

    /// <summary>
    /// A number.
    /// </summary>
    Number = 1 << 4,

    /// <summary>
    /// A boolean.
    /// </summary>
    Boolean = 1 << 5,

    /// <summary>
    /// A null.
    /// </summary>
    Null = 1 << 6,
}
