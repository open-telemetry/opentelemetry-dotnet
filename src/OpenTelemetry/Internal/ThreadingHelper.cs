// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Threading;

namespace OpenTelemetry.Internal;

/// <summary>
/// Provides helpers for determining whether threads are available and for
/// temporarily overriding that detection. The override is primarily used in
/// tests to ensure both threading modes stay covered.
/// </summary>
internal static class ThreadingHelper
{
    private static readonly AsyncLocal<bool?> ThreadingDisabledOverride = new();

    /// <summary>
    /// Sets a scoped override indicating whether threading should be treated as
    /// disabled. Returns an <see cref="IDisposable"/> that restores the
    /// previous value when disposed.
    /// </summary>
    internal static IDisposable BeginThreadingOverride(bool isThreadingDisabled)
    {
        var scope = new ThreadingOverrideScope(ThreadingDisabledOverride.Value);
        ThreadingDisabledOverride.Value = isThreadingDisabled;
        return scope;
    }

    /// <summary>
    /// Determines whether threading should be considered unavailable for the
    /// current context, honoring any scoped overrides.
    /// </summary>
    internal static bool IsThreadingDisabled()
    {
        if (ThreadingDisabledOverride.Value.HasValue)
        {
            return ThreadingDisabledOverride.Value.Value;
        }

        // if the threadpool isn't using threads assume they aren't enabled
        ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);

        return workerThreads == 1 && completionPortThreads == 1;
    }

    private sealed class ThreadingOverrideScope : IDisposable
    {
        private readonly bool? previous;
        private bool disposed;

        public ThreadingOverrideScope(bool? previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                ThreadingDisabledOverride.Value = this.previous;
                this.disposed = true;
            }
        }
    }
}
