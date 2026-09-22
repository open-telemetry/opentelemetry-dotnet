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
    // Keep the cache bounded while covering typical nested Activity depths.
    private const int MaxCachedReferenceCount = 16;

    // An immutable value which controls whether instrumentation should be suppressed (disabled).
    // * null: instrumentation is not suppressed
    // * [int.MinValue, -1]: instrumentation is always suppressed
    // * [1, int.MaxValue]: instrumentation is suppressed in a reference-counting mode
    // AsyncLocal flows reference values into child execution contexts by reference. The
    // states must remain immutable so count changes in one context cannot leak into another.
    private static readonly SuppressionState AlwaysSuppressed = new(-1);

    // Reuse the common reference counts to avoid allocating a state object for each update.
    private static readonly SuppressionState[] ReferenceCountingStates = CreateReferenceCountingStates();
    private static readonly RuntimeContextSlot<SuppressionState?> Slot =
        RuntimeContext.RegisterSlot<SuppressionState?>("otel.suppress_instrumentation");

    private readonly SuppressionState? previousState;
    private bool disposed;

    internal SuppressInstrumentationScope(bool value = true)
    {
        this.previousState = Slot.Get();
        Slot.Set(value ? AlwaysSuppressed : null);
    }

    internal static bool IsSuppressed => Slot.Get() != null;

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
        return new SuppressInstrumentationScope(value);
    }

    /// <summary>
    /// Enters suppression mode.
    /// If suppression mode is enabled (state has a negative depth), do nothing.
    /// If suppression mode is not enabled (state is null), enter reference-counting suppression mode.
    /// If suppression mode is enabled (state has a positive depth), increment the ref count.
    /// </summary>
    /// <returns>The updated suppression slot value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Enter()
    {
        var currentDepth = Slot.Get()?.Depth ?? 0;

        if (currentDepth >= 0)
        {
            Slot.Set(GetState(++currentDepth));
        }

        return currentDepth;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            Slot.Set(this.previousState);
            this.disposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IncrementIfTriggered()
    {
        var currentDepth = Slot.Get()?.Depth ?? 0;

        if (currentDepth > 0)
        {
            Slot.Set(GetState(++currentDepth));
        }

        return currentDepth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int DecrementIfTriggered()
    {
        var currentDepth = Slot.Get()?.Depth ?? 0;

        if (currentDepth > 0)
        {
            Slot.Set(--currentDepth == 0 ? null : GetState(currentDepth));
        }

        return currentDepth;
    }

    private static SuppressionState[] CreateReferenceCountingStates()
    {
        var states = new SuppressionState[MaxCachedReferenceCount];

        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new SuppressionState(i + 1);
        }

        return states;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SuppressionState GetState(int depth)
    {
        var stateIndex = depth - 1;

        return (uint)stateIndex < (uint)ReferenceCountingStates.Length
            ? ReferenceCountingStates[stateIndex]
            : new SuppressionState(depth);
    }

    private sealed class SuppressionState(int depth)
    {
        public int Depth { get; } = depth;
    }
}
