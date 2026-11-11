// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Releases components that were created but never handed to a provider.
/// </summary>
/// <remarks>
/// A failure while cleaning up is logged and never thrown, so that it cannot replace the failure
/// that made the cleanup necessary.
/// </remarks>
internal static class ComponentCleanup
{
    /// <summary>
    /// Runs <paramref name="cleanup"/>, logging any exception it throws.
    /// </summary>
    /// <param name="component">The component being cleaned up, named in the log entry.</param>
    /// <param name="cleanup">The cleanup to run.</param>
    internal static void InvokeAndLogFailure(object component, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.ComponentCleanupFailed(component.GetType().FullName ?? component.GetType().Name, ex);
        }
    }

    /// <summary>
    /// Shuts down <paramref name="sampler"/> and disposes it when it is disposable.
    /// </summary>
    /// <param name="sampler">The sampler to release.</param>
    internal static void ShutDownAndDispose(Sampler sampler)
    {
        InvokeAndLogFailure(sampler, () => sampler.Shutdown(0));

        if (sampler is IDisposable disposable)
        {
            InvokeAndLogFailure(sampler, disposable.Dispose);
        }
    }
}
