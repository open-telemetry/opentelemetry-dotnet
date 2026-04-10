// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Context;

namespace OpenTelemetry;

/// <summary>
/// Contains methods managing instrumentation of internal operations.
/// </summary>
public sealed class SuppressInstrumentationScope : IDisposable
{
    private static readonly int BeginPoolMaxSize = Environment.ProcessorCount * 2;

    // An integer value which controls whether instrumentation should be suppressed (disabled).
    // * null: instrumentation is not suppressed
    // * Depth = [int.MinValue, -1]: instrumentation is always suppressed
    // * Depth = [1, int.MaxValue]: instrumentation is suppressed in a reference-counting mode
    private static readonly RuntimeContextSlot<SuppressInstrumentationScope?> Slot = RuntimeContext.RegisterSlot<SuppressInstrumentationScope?>("otel.suppress_instrumentation");

    // Thread-local pool for Begin() scopes. Bounded to avoid unbounded growth.
    [ThreadStatic]
    private static Stack<SuppressInstrumentationScope>? beginPool;

    // Thread-local no-op singleton returned by nested Begin(true) calls when already in
    // always-suppress mode. Avoids both a heap allocation and an AsyncLocal write.
    [ThreadStatic]
    private static NoOpDisposable? noop;

    // Thread-local cached scope for the Enter() path. Reused whenever Enter() is
    // called with no active scope, eliminating the allocation on the hot activity path.
    [ThreadStatic]
    private static SuppressInstrumentationScope? enterCache;

#pragma warning disable CA2213 // Disposable fields should be disposed
    private SuppressInstrumentationScope? previousScope;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private bool disposed;
    private bool pooled;

    private SuppressInstrumentationScope()
    {
    }

    internal static bool IsSuppressed => (Slot.Get()?.Depth ?? 0) != 0;

    internal int Depth { get; private set; }

    /// <summary>
    /// Begins a new scope in which instrumentation is suppressed (disabled).
    /// </summary>
    /// <param name="value">Value indicating whether to suppress instrumentation.</param>
    /// <returns>Object to dispose to end the scope.</returns>
    /// <remarks>
    /// This is typically used to prevent infinite loops created by
    /// collection of internal operations, such as exporting traces over HTTP.
    /// <code>
    ///     public override async Task&lt;ExportResult&gt; ExportAsync(
    ///         IEnumerable&lt;Activity&gt; batch,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         using (SuppressInstrumentationScope.Begin())
    ///         {
    ///             // Instrumentation is suppressed (i.e., Sdk.SuppressInstrumentation == true)
    ///         }
    ///
    ///         // Instrumentation is not suppressed (i.e., Sdk.SuppressInstrumentation == false)
    ///     }
    /// </code>
    /// </remarks>
    public static IDisposable Begin(bool value = true)
    {
        // When already in always-suppress mode and asked to suppress again, the
        // slot state is unchanged - return a no-op disposable to skip both the allocation and
        // the AsyncLocal write.
        if (value && Slot.Get()?.Depth < 0)
        {
            return noop ??= new NoOpDisposable();
        }

        // Rent a scope from the thread-local pool, or allocate a fresh one.
        var pool = beginPool;
        var scope = pool is { Count: > 0 } ? pool.Pop() : new SuppressInstrumentationScope();
        scope.Initialize(value);
        return scope;
    }

    /// <summary>
    /// Enters suppression mode.
    /// If suppression mode is enabled (slot.Depth is a negative integer), do nothing.
    /// If suppression mode is not enabled (slot is null), enter reference-counting suppression mode.
    /// If suppression mode is enabled (slot.Depth is a positive integer), increment the ref count.
    /// </summary>
    /// <returns>The updated suppression slot value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Enter()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            // Reuse the thread-local cached scope to avoid allocating
            var scope = enterCache ??= new SuppressInstrumentationScope();
            scope.previousScope = null;
            scope.Depth = 1;
            Slot.Set(scope);
            return 1;
        }

        var currentDepth = currentScope.Depth;

        if (currentDepth >= 0)
        {
            currentScope.Depth = ++currentDepth;
        }

        return currentDepth;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            Slot.Set(this.previousScope);
            this.disposed = true;

            // Return the scope to the thread-local pool for reuse by future Begin() calls.
            if (this.pooled)
            {
                var pool = beginPool ??= new Stack<SuppressInstrumentationScope>(BeginPoolMaxSize);
                if (pool.Count < BeginPoolMaxSize)
                {
                    this.previousScope = null; // release the reference before returning to pool
                    pool.Push(this);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IncrementIfTriggered()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            return 0;
        }

        var currentDepth = currentScope.Depth;

        if (currentScope.Depth > 0)
        {
            currentScope.Depth = ++currentDepth;
        }

        return currentDepth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int DecrementIfTriggered()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            return 0;
        }

        var currentDepth = currentScope.Depth;

        if (currentScope.Depth > 0)
        {
            if (--currentDepth == 0)
            {
                Slot.Set(currentScope.previousScope);
            }
            else
            {
                currentScope.Depth = currentDepth;
            }
        }

        return currentDepth;
    }

    private void Initialize(bool value)
    {
        this.previousScope = Slot.Get();

        this.Depth = value ? -1 : 0;
        this.disposed = false;
        this.pooled = true;

        Slot.Set(this);
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}
