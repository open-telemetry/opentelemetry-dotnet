// <copyright file="Tracer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace
{
    internal sealed class Tracer : ITracer
    {
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfiguration tracerConfiguration;

        static Tracer()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        /// <summary>
        /// Creates an instance of <see cref="Tracer"/>.
        /// </summary>
        /// <param name="spanProcessor">Span processor.</param>
        /// <param name="tracerConfiguration">Trace configuration.</param>
        /// <param name="binaryFormat">Binary format context propagator.</param>
        /// <param name="textFormat">Text format context propagator.</param>
        /// <param name="libraryResource">Resource describing the instrumentation library.</param>
        internal Tracer(SpanProcessor spanProcessor, TracerConfiguration tracerConfiguration, IBinaryFormat binaryFormat, ITextFormat textFormat, Resource libraryResource)
        {
            this.spanProcessor = spanProcessor ?? throw new ArgumentNullException(nameof(spanProcessor));
            this.tracerConfiguration = tracerConfiguration ?? throw new ArgumentNullException(nameof(tracerConfiguration));
            this.BinaryFormat = binaryFormat ?? throw new ArgumentNullException(nameof(binaryFormat));
            this.TextFormat = textFormat ?? throw new ArgumentNullException(nameof(textFormat));
            this.LibraryResource = libraryResource ?? throw new ArgumentNullException(nameof(libraryResource));
        }

        public Resource LibraryResource { get; }

        /// <inheritdoc/>
        public ISpan CurrentSpan => (ISpan)Span.Current ?? BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat { get; }

        /// <inheritdoc/>
        public ITextFormat TextFormat { get; }

        public IDisposable WithSpan(ISpan span, bool endSpanOnDispose)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            if (span is Span spanImpl)
            {
                return spanImpl.BeginScope(endSpanOnDispose);
            }

            return NoopDisposable.Instance;
        }

        /// <inheritdoc/>
        public ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            return Span.CreateRoot(operationName, kind, options, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (parent == null)
            {
                parent = this.CurrentSpan;
            }

            return Span.CreateFromParentSpan(operationName, parent, kind, options, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (parent != null)
            {
                return Span.CreateFromParentContext(operationName, parent, kind, options, this.tracerConfiguration,
                    this.spanProcessor, this.LibraryResource);
            }

            return Span.CreateRoot(operationName, kind, options, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
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

            if (activity.StartTimeUtc == default || activity.Duration != default)
            {
                throw new ArgumentException(
                    "Current Activity is not running: it has not been started or has been stopped");
            }

            return Span.CreateFromActivity(operationName, activity, kind, links, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }
    }
}
