// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The stability of a location in a configuration document.
/// </summary>
internal enum SchemaStability
{
    /// <summary>
    /// The location is covered by the configuration versioning guarantees.
    /// </summary>
    Stable = 0,

    /// <summary>
    /// The location is, or is nested in, a property marked as under development.
    /// </summary>
    Experimental = 1,
}
