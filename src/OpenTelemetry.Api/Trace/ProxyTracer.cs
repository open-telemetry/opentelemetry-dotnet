// <copyright file="ProxyTracer.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Proxy tracer.
    /// </summary>
    internal sealed class ProxyTracer : Tracer
    {
        private readonly Tracer defaultTracer;
        private Tracer realTracer;

        public ProxyTracer(Tracer defaultTracer)
        {
            this.defaultTracer = defaultTracer;
        }

        /// <inheritdoc/>
        public override TelemetrySpan CurrentSpan => this.realTracer?.CurrentSpan ?? this.defaultTracer.CurrentSpan;

        /// <inheritdoc/>
        public override IDisposable WithSpan(TelemetrySpan span, bool endOnDispose)
        {
            return this.realTracer != null ? this.realTracer.WithSpan(span, endOnDispose) : this.defaultTracer.WithSpan(span, endOnDispose);
        }

        public override TelemetrySpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            return this.realTracer != null ? this.realTracer.StartRootSpan(operationName, kind, options) : this.defaultTracer.StartRootSpan(operationName, kind, options);
        }

        public override TelemetrySpan StartSpan(string operationName, TelemetrySpan parent, SpanKind kind, SpanCreationOptions options)
        {
            return this.realTracer != null ? this.realTracer.StartSpan(operationName, parent, kind, options) : this.defaultTracer.StartSpan(operationName, parent, kind, options);
        }

        public override TelemetrySpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            return this.realTracer != null ? this.realTracer.StartSpan(operationName, parent, kind, options) : this.defaultTracer.StartSpan(operationName, parent, kind, options);
        }

        public override TelemetrySpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            return this.realTracer != null ? this.realTracer.StartSpanFromActivity(operationName, activity, kind, links) : this.defaultTracer.StartSpanFromActivity(operationName, activity, kind, links);
        }

        public void UpdateTracer(Tracer realTracer)
        {
            if (this.realTracer != null)
            {
                return;
            }

            // just in case user calls init concurrently
            Interlocked.CompareExchange(ref this.realTracer, realTracer, null);
        }
    }
}