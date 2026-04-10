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
    private const int BeginPoolMaxSize = 8;

    // An integer value which controls whether instrumentation should be suppressed (disabled).
    // * null: instrumentation is not suppressed
    // * Depth = [int.MinValue, -1]: instrumentation is always suppressed
    // * Depth = [1, int.MaxValue]: instrumentation is suppressed in a reference-counting mode
    private static readonly RuntimeContextSlot<SuppressInstrumentationScope?> Slot = RuntimeContext.RegisterSlot<SuppressInstrumentationScope?>("otel.suppress_instrumentation");

    private static readonly NoOpDisposable Noop = new();

    // Thread-local pool for Begin() scopes. Bounded to avoid unbounded growth.
    [ThreadStatic]
    private static Stack<SuppressInstrumentationScope>? beginPool;

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
            return Noop;
        }

        // Rent a scope from the thread-local pool, or allocate a fresh one.
        var pool = beginPool;
        var scope = pool is { Count: > 0 } ? pool.Pop() : new SuppressInstrumentationScope();
        scope.Initialize(value);

        // Return a token wrapper rather than the scope itself. The token nulls its
        // scope reference after the first Dispose(), so any stale reference to the
        // token becomes a safe no-op even if the underlying scope has since been
        // re-rented from the pool and re-initialized for a different caller.
        return new ScopeToken(scope);
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
                var pool = beginPool ??= new Stack<SuppressInstrumentationScope>();
                if (pool.Count < BeginPoolMaxSize)
                {
                    this.previousScope = null; // Release the reference before returning to pool
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

    /// <summary>
    /// Thin wrapper returned by Begin(). Holds a nullable reference to the underlying
    /// scope and clears it on first Dispose(), making all subsequent calls a safe no-op.
    /// This prevents a stale IDisposable reference from accidentally affecting a scope
    /// instance that has been returned to the pool and re-rented for a different caller.
    /// </summary>
    private sealed class ScopeToken : IDisposable
    {
        private SuppressInstrumentationScope? scope;

        internal ScopeToken(SuppressInstrumentationScope scope)
        {
            this.scope = scope;
        }

        public void Dispose()
        {
            var s = this.scope;
            if (s is not null)
            {
                this.scope = null;
                s.Dispose();
            }
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}
