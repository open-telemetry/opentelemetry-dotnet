// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Platform-safe entry point used by provider builders to register with self-diagnostics.
/// </summary>
internal static class SelfDiagnosticsRegistration
{
    /// <summary>
    /// Registers a provider's live self-diagnostics options. On platforms where
    /// self-diagnostics is not supported this is a no-op.
    /// </summary>
    /// <param name="monitor">The provider's live self-diagnostics options.</param>
    /// <returns>A lease that relinquishes ownership when the provider is disposed.</returns>
    internal static IDisposable Register(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
    {
#if NET
        if (OperatingSystem.IsBrowser())
        {
            return NoopRegistration.Instance;
        }
#endif

        return SelfDiagnostics.Initialize(monitor);
    }

#if NET
    private sealed class NoopRegistration : IDisposable
    {
        public static readonly NoopRegistration Instance = new();

        public void Dispose()
        {
        }
    }
#endif
}
