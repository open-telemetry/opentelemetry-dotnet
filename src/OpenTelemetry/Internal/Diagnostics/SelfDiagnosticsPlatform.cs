// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Identifies the platform-specific self-diagnostics log directory convention.
/// </summary>
internal enum SelfDiagnosticsPlatform
{
    /// <summary>
    /// The Windows directory convention.
    /// </summary>
    Windows,

    /// <summary>
    /// The macOS directory convention.
    /// </summary>
    MacOS,

    /// <summary>
    /// The Unix-like directory convention.
    /// </summary>
    Unix,
}
