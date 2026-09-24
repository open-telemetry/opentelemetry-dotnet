// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The kinds of value represented by a <see cref="ConfigProperties"/> property.
/// </summary>
#pragma warning disable CA1720 // Enum members intentionally use the configuration value-kind names.
public enum ConfigValueKind
{
    /// <summary>
    /// A null value.
    /// </summary>
    Null = 0,

    /// <summary>
    /// A string value.
    /// </summary>
    String,

    /// <summary>
    /// A boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// An integer value.
    /// </summary>
    Integer,

    /// <summary>
    /// A double-precision floating-point value. The configuration specification describes this scalar
    /// as "double precision floating point" and leaves the naming to whatever is idiomatic for the
    /// language, so the member is named for the stored <see cref="double"/> representation rather than
    /// for the YAML <c>!!float</c> tag that resolves to it.
    /// </summary>
    Double,

    /// <summary>
    /// A nested mapping, represented as a <see cref="ConfigProperties"/>.
    /// </summary>
    Mapping,

    /// <summary>
    /// A sequence of configuration values.
    /// </summary>
    Sequence,
}
#pragma warning restore CA1720 // Identifiers should not contain type names.
