// <copyright file="ScopeManagerShim.cs" company="OpenTelemetry Authors">
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

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using global::OpenTracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing
{
    public sealed class ScopeManagerShim : IScopeManager
    {
        private static readonly ConditionalWeakTable<TelemetrySpan, global::OpenTracing.IScope> SpanScopeTable = new ConditionalWeakTable<TelemetrySpan, global::OpenTracing.IScope>();

        private readonly Tracer tracer;

#if DEBUG
        private int spanScopeTableCount;
#endif

        public ScopeManagerShim(Trace.Tracer tracer)
        {
            this.tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

#if DEBUG
        public int SpanScopeTableCount => this.spanScopeTableCount;
#endif

        /// <inheritdoc/>
        public global::OpenTracing.IScope Active
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
        public global::OpenTracing.IScope Activate(ISpan span, bool finishSpanOnDispose)
        {
            if (!(span is SpanShim shim))
            {
                throw new ArgumentException("span is not a valid SpanShim object");
            }

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
                    scope.Dispose();
                });

            SpanScopeTable.Add(shim.Span, instrumentation);
#if DEBUG
            Interlocked.Increment(ref this.spanScopeTableCount);
#endif

            return instrumentation;
        }

        private class ScopeInstrumentation : global::OpenTracing.IScope
        {
            private readonly Action disposeAction;

            public ScopeInstrumentation(TelemetrySpan span, Action disposeAction = null)
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
}
