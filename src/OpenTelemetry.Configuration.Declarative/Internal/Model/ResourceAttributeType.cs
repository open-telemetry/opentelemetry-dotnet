// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A resource attribute type from the declarative configuration schema.
/// </summary>
internal enum ResourceAttributeType
{
    /// <summary>A string.</summary>
    String,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>An integer.</summary>
    Integer,

    /// <summary>A double-precision number.</summary>
    Double,

    /// <summary>An array of strings.</summary>
    StringArray,

    /// <summary>An array of booleans.</summary>
    BooleanArray,

    /// <summary>An array of integers.</summary>
    IntegerArray,

    /// <summary>An array of double-precision numbers.</summary>
    DoubleArray,
}
