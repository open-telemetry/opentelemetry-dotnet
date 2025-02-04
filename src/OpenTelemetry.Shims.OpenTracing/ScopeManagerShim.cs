// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing;

internal sealed class ScopeManagerShim : IScopeManager
{
    private static readonly ConditionalWeakTable<TelemetrySpan, IScope> SpanScopeTable = new();

#if DEBUG
    private int spanScopeTableCount;

    public int SpanScopeTableCount => this.spanScopeTableCount;
#endif

    /// <inheritdoc/>
    public IScope? Active
    {
        get
        {
            var currentSpan = Tracer.CurrentSpan;
            if (currentSpan == null || !currentSpan.Context.IsValid)
            {
                return null;
            }

            if (SpanScopeTable.TryGetValue(currentSpan, out var openTracingScope))
            {
                return openTracingScope;
            }

            return new ScopeInstrumentation(currentSpan);
        }
    }

    /// <inheritdoc/>
    public IScope Activate(ISpan span, bool finishSpanOnDispose)
    {
        var shim = Guard.ThrowIfNotOfType<SpanShim>(span);

        var scope = Tracer.WithSpan(shim.Span);

        var instrumentation = new ScopeInstrumentation(
            shim.Span,
            () =>
            {
                var removed = SpanScopeTable.Remove(shim.Span);
#if DEBUG
                if (removed)
                {
                    Interlocked.Decrement(ref this.spanScopeTableCount);
                }
#endif
                scope!.Dispose();
            });

        // not recorded span is static TelemetrySpan.NoopInstance
        // we do not return the instrumentation if the span is not recorded anyway
        if (shim.Span.IsRecording)
        {
            SpanScopeTable.Add(shim.Span, instrumentation);
#if DEBUG
            Interlocked.Increment(ref this.spanScopeTableCount);
#endif
        }

        return instrumentation;
    }

    private sealed class ScopeInstrumentation : IScope
    {
        private readonly Action? disposeAction;

        public ScopeInstrumentation(TelemetrySpan span, Action? disposeAction = null)
        {
            this.Span = new SpanShim(span);
            this.disposeAction = disposeAction;
        }

        /// <inheritdoc/>
        public ISpan Span { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.disposeAction?.Invoke();
        }
    }
}
