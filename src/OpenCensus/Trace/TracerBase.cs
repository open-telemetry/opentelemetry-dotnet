// <copyright file="TracerBase.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System;
    using OpenCensus.Common;
    using OpenCensus.Trace.Internal;

    public abstract class TracerBase : ITracer
    {
        private static readonly NoopTracer NoopTracerInstance = new NoopTracer();

        public ISpan CurrentSpan
        {
            get
            {
                ISpan currentSpan = CurrentSpanUtils.CurrentSpan;
                return currentSpan ?? BlankSpan.Instance;
            }
        }

        internal static NoopTracer NoopTracer
        {
            get
            {
                return NoopTracerInstance;
            }
        }

        public IScope WithSpan(ISpan span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return CurrentSpanUtils.WithSpan(span, false);
        }

        // public final Runnable withSpan(Span span, Runnable runnable)
        // public final <C> Callable<C> withSpan(Span span, final Callable<C> callable)
        public ISpanBuilder SpanBuilder(string spanName, SpanKind spanKind = SpanKind.Unspecified)
        {
            return this.SpanBuilderWithExplicitParent(spanName, spanKind, CurrentSpanUtils.CurrentSpan);
        }

        public abstract ISpanBuilder SpanBuilderWithExplicitParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpan parent = null);

        public abstract ISpanBuilder SpanBuilderWithRemoteParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpanContext remoteParentSpanContext = null);
    }
}
