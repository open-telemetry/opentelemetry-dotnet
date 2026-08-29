// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Defines a method that initializes telemetry for a host.
/// </summary>
public interface ITelemetryHostInitializer
{
    /// <summary>
    /// Initializes telemetry for a host.
    /// </summary>
    void Initialize();
}
