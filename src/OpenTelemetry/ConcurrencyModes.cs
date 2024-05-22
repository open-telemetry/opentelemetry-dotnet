// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Describes the concurrency mode of an OpenTelemetry extension.
/// </summary>
[Flags]
public enum ConcurrencyModes : byte
{
    /*
    0 0 0 0 0 0 0 0
    | | | | | | | |
    | | | | | | | +--- Reentrant
    | | | | | | +----- Multithreaded
    | | | | | +------- (reserved)
    | | | | +--------- (reserved)
    | | | +----------- (reserved)
    | | +------------- (reserved)
    | +--------------- (reserved)
    +----------------- (reserved)
    */

    /// <summary>
    /// Reentrant, the component can be invoked recursively without resulting
    /// a deadlock or infinite loop.
    /// </summary>
    Reentrant = 0b1,

    /// <summary>
    /// Multithreaded, the component can be invoked concurrently across
    /// multiple threads.
    /// </summary>
    Multithreaded = 0b10,
}
