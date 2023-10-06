// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

// The ActivityContext class is in the System.Diagnostics namespace.
// These extension methods on ActivityContext are intentionally not placed in the
// same namespace as Activity to prevent name collisions in the future.
// The OpenTelemetry namespace is used because ActivityContext applies to all types
// of telemetry data - i.e. traces, metrics, and logs.
namespace OpenTelemetry;

/// <summary>
/// Extension methods on ActivityContext.
/// </summary>
public static class ActivityContextExtensions
{
    /// <summary>
    /// Returns a bool indicating if a ActivityContext is valid or not.
    /// </summary>
    /// <param name="ctx">ActivityContext.</param>
    /// <returns>whether the context is a valid one or not.</returns>
    public static bool IsValid(this ActivityContext ctx)
    {
        return ctx != default;
    }
}
