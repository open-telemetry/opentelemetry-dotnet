// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// How a schema node treats properties it does not list.
/// </summary>
internal enum AdditionalPropertiesKind
{
    /// <summary>
    /// Any additional property is permitted.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// No additional property is permitted.
    /// </summary>
    Forbidden = 1,

    /// <summary>
    /// Additional properties are permitted and described by a schema.
    /// </summary>
    Schema = 2,
}
