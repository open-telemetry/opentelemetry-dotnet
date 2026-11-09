// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The smallest value an integer property accepts.
/// </summary>
internal enum IntegerMinimum
{
    /// <summary>
    /// Zero is accepted, so the value must be non-negative.
    /// </summary>
    Zero = 0,

    /// <summary>
    /// Zero is rejected, so the value must be positive.
    /// </summary>
    One = 1,
}
