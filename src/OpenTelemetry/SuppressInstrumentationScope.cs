// <copyright file="SuppressInstrumentationScope.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;
using OpenTelemetry.Context;

namespace OpenTelemetry;

public sealed class SuppressInstrumentationScope : IDisposable
{
    // An integer value which controls whether instrumentation should be suppressed (disabled).
    // * null: instrumentation is not suppressed
    // * Depth = [int.MinValue, -1]: instrumentation is always suppressed
    // * Depth = [1, int.MaxValue]: instrumentation is suppressed in a reference-counting mode
    private static readonly RuntimeContextSlot<SuppressInstrumentationScope?> Slot = RuntimeContext.RegisterSlot<SuppressInstrumentationScope?>("otel.suppress_instrumentation");

    private readonly SuppressInstrumentationScope? previousScope;
    private bool disposed;

    internal SuppressInstrumentationScope(bool value = true)
    {
        this.previousScope = Slot.Get();
        this.Depth = value ? -1 : 0;
        Slot.Set(this);
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
        return new SuppressInstrumentationScope(value);
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
            Slot.Set(
                new SuppressInstrumentationScope()
                {
                    Depth = 1,
                });

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
}
