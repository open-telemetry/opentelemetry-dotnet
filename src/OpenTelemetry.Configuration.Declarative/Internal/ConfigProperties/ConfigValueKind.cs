// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// The kinds of value a <see cref="ConfigValue"/> can hold.
/// </summary>
/// <remarks>
/// The scalar members cover the same set of types as the YAML 1.2 core schema tags, but are restated
/// here rather than reused from the YAML layer so that the value model stays independent of the source format.
/// A future JSON or OpAMP source maps onto the same kinds.
/// </remarks>
internal enum ConfigValueKind
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
    /// A sequence of <see cref="ConfigValue"/> items.
    /// </summary>
    Sequence,
}
