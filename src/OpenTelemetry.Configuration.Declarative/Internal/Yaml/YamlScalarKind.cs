// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The scalar tags defined by the YAML 1.2 core schema.
/// </summary>
internal enum YamlScalarKind
{
    /// <summary>A string scalar.</summary>
    String,

    /// <summary>A null scalar.</summary>
    Null,

    /// <summary>A boolean scalar.</summary>
    Boolean,

    /// <summary>An integer scalar.</summary>
    Integer,

    /// <summary>A floating-point scalar.</summary>
    Float,
}
