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
#if DEBUG
using System.Threading;
#endif
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing
{
    internal sealed class ScopeManagerShim : IScopeManager
    {
        private static readonly ConditionalWeakTable<TelemetrySpan, IScope> SpanScopeTable = new ConditionalWeakTable<TelemetrySpan, IScope>();

        private readonly Tracer tracer;

#if DEBUG
        private int spanScopeTableCount;
#endif

        public ScopeManagerShim(Tracer tracer)
        {
            Guard.ThrowIfNull(tracer, nameof(tracer));

            this.tracer = tracer;
        }

#if DEBUG
        public int SpanScopeTableCount => this.spanScopeTableCount;
#endif

        /// <inheritdoc/>
        public IScope Active
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
            var shim = Guard.ThrowIfNotOfType<SpanShim>(span, nameof(span));

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

        private class ScopeInstrumentation : IScope
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
