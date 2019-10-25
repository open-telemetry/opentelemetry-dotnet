// <copyright file="LoggingTracer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    /// <summary>
    /// Logger tracer.
    /// </summary>
    public class LoggingTracer : ITracer
    {
        private readonly string prefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingTracer"/> class.
        /// </summary>
        internal LoggingTracer(string prefix)
        {
            this.prefix = $"Tracer({prefix})";
            Logger.Log("Tracer.ctor()");
        }

        /// <inheritdoc/>
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat => new LoggingBinaryFormat();

        /// <inheritdoc/>
        public ITextFormat TextFormat => new LoggingTextFormat();

        /// <inheritdoc/>
        public IDisposable WithSpan(ISpan span, bool endOnDispose)
        {
            Logger.Log($"{this.prefix}.WithSpan {endOnDispose}");
            return new CurrentSpanUtils.LoggingScope(span);
        }

        /// <inheritdoc/>
        public ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            Logger.Log($"{this.prefix}.StartRootSpan({operationName}, {kind}, {options.StartTimestamp:o}, {options.LinksFactory}, {options.Links})");
            return new LoggingSpan(operationName, kind);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options)
        {
            Logger.Log($"{this.prefix}.StartSpan({operationName}, {parent.GetType().Name}, {kind}, {options.StartTimestamp:o}, {options.LinksFactory}, {options.Links})");
            return new LoggingSpan(operationName, kind);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            Logger.Log($"{this.prefix}.StartSpan({operationName}, {parent.GetType().Name}, {kind}, {options.StartTimestamp:o}, {options.LinksFactory}, {options.Links})");
            return new LoggingSpan(operationName, kind);
        }

        /// <inheritdoc/>
        public ISpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            Logger.Log($"{this.prefix}.StartSpanFromActivity({operationName}, {activity.OperationName}, {kind}, {links})");
            return new LoggingSpan(operationName, kind);
        }
    }
}
