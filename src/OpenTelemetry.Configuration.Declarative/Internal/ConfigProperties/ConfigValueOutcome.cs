// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// The four outcomes of a typed read from a <see cref="ConfigProperties"/>.
/// </summary>
/// <remarks>
/// The OpenTelemetry configuration specification requires a key that is present with a null
/// value to be distinguishable from one that is not set, because the two select different
/// behaviour when a component is created. A value of the wrong type is a third distinct case,
/// which a component provider needs in order to report an error rather than silently accept a
/// default.
/// </remarks>
internal enum ConfigValueOutcome
{
    /// <summary>
    /// The key did not appear in the mapping.
    /// </summary>
    Absent,

    /// <summary>
    /// The key appeared with a null value.
    /// </summary>
    PresentNull,

    /// <summary>
    /// The key appeared with a value of the requested type.
    /// </summary>
    Present,

    /// <summary>
    /// The key appeared with a value that is not of the requested type.
    /// </summary>
    TypeMismatch,
}
