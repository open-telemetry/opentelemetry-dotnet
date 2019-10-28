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
    /// No-op tracer.
    /// </summary>
    internal sealed class ProxyTracer : ITracer
    {
        private static readonly IDisposable NoopScope = new NoopDisposable();
        private readonly IBinaryFormat binaryFormat = new BinaryFormat();
        private readonly ITextFormat textFormat = new TraceContextFormat();

        private ITracer realTracer;

        /// <inheritdoc/>
        public ISpan CurrentSpan => this.realTracer?.CurrentSpan ?? BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat => this.realTracer?.BinaryFormat ?? this.binaryFormat;

        /// <inheritdoc/>
        public ITextFormat TextFormat => this.realTracer?.TextFormat ?? this.textFormat;

        /// <inheritdoc/>
        public IDisposable WithSpan(ISpan span, bool endOnDispose)
        {
            return this.realTracer != null ? this.realTracer.WithSpan(span, endOnDispose) : NoopScope;
        }

        public ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            return this.realTracer != null ? this.realTracer.StartRootSpan(operationName, kind, options) : BlankSpan.Instance;
        }

        public ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            return this.realTracer != null ? this.realTracer.StartSpan(operationName, parent, kind, options) : BlankSpan.Instance;
        }

        public ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            return this.realTracer != null ? this.realTracer.StartSpan(operationName, parent, kind, options) : BlankSpan.Instance;
        }

        public ISpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (activity.IdFormat != ActivityIdFormat.W3C)
            {
                throw new ArgumentException("Current Activity is not in W3C format");
            }

            return this.realTracer != null ? this.realTracer.StartSpanFromActivity(operationName, activity, kind, links) : BlankSpan.Instance;
        }

        public void UpdateTracer(ITracer realTracer)
        {
            if (this.realTracer != null)
            {
                return;
            }

            // just in case user calls init concurrently
            Interlocked.CompareExchange(ref this.realTracer, realTracer, null);
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
