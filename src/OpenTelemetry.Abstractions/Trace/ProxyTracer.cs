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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Threading;
    using OpenTelemetry.Context.Propagation;

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
        public IDisposable WithSpan(ISpan span)
        {
            return this.realTracer != null ? this.realTracer.WithSpan(span) : NoopScope;
        }

        /// <inheritdoc/>
        public ISpanBuilder SpanBuilder(string spanName)
        {
            return this.realTracer != null ? this.realTracer.SpanBuilder(spanName) : new NoopSpanBuilder(spanName);
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
