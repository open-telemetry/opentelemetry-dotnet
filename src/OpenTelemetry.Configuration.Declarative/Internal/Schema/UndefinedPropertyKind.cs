// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Why a property the schema does not define was retained rather than rejected.
/// </summary>
internal enum UndefinedPropertyKind
{
    /// <summary>
    /// The reason the property was retained is unspecified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The property is under development, where the schema may change in any minor version.
    /// </summary>
    Experimental = 1,

    /// <summary>
    /// The document declares a newer schema version than the one this package understands.
    /// </summary>
    NewerFileFormat = 2,

    /// <summary>
    /// The property is at the document root, where the schema permits additional properties.
    /// </summary>
    Root = 3,
}
