// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// The kinds of value a <see cref="ConfigValue"/> can hold.
/// </summary>
/// <remarks>
/// The scalar members mirror the YAML 1.2 core schema tags, but are restated here rather than
/// reused from the YAML layer so that the value model stays independent of the source format.
/// A future JSON or OpAMP source maps onto the same kinds.
/// </remarks>
internal enum ConfigValueKind
{
    /// <summary>
    /// A null value.
    /// </summary>
    Null,

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
    /// A floating-point value.
    /// </summary>
    Float,

    /// <summary>
    /// A nested mapping, represented as a <see cref="ConfigProperties"/>.
    /// </summary>
    Mapping,

    /// <summary>
    /// A sequence of <see cref="ConfigValue"/> items.
    /// </summary>
    Sequence,
}
