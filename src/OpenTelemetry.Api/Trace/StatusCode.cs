// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Trace;

/// <summary>
/// Canonical result code of span execution.
/// </summary>
public enum StatusCode
{
    /// <summary>
    /// The default status.
    /// </summary>
    Unset = 0,

    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Ok = 1,

    /// <summary>
    /// The operation contains an error.
    /// </summary>
    Error = 2,
}
