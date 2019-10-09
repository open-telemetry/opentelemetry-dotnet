// <copyright file="NoopTracer.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// No-op tracer.
    /// </summary>
    public sealed class NoopTracer : ITracer
    {
        /// <summary>
        /// Instance of the noop tracer.
        /// </summary>
        public static readonly NoopTracer Instance = new NoopTracer();

        private static readonly IDisposable NoopScope = new NoopDisposable();

        internal NoopTracer()
        {
        }

        /// <inheritdoc/>
        public ISpan CurrentSpan => BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat => new BinaryFormat();

        /// <inheritdoc/>
        public ITextFormat TextFormat => new TraceContextFormat();

        /// <inheritdoc/>
        public IDisposable WithSpan(ISpan span)
        {
            return NoopScope;
        }

        // TODO validation

        public ISpan CreateRootSpan(string operationName)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateRootSpan(string operationName, SpanKind kind)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateRootSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateRootSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, SpanKind kind)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, ISpan parent)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, in SpanContext parent)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpanFromActivity(string operationName, Activity activity)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpanFromActivity(string operationName, Activity activity, SpanKind kind)
        {
            return BlankSpan.Instance;
        }

        public ISpan CreateSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            return BlankSpan.Instance;
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
