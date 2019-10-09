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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Configuration;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;
    using OpenTelemetry.Utils;

    /// <inheritdoc/>
    public sealed class Tracer : ITracer
    {
        private readonly SpanProcessor spanProcessor;

        static Tracer()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        /// <summary>
        /// Creates an instance of <see cref="ITracer"/>.
        /// </summary>
        /// <param name="spanProcessor">Span processor.</param>
        /// <param name="tracerConfiguration">Trace configuration.</param>
        /// <param name="libraryResource">Resource describing the instrumentation library.</param>
        public Tracer(SpanProcessor spanProcessor, TracerConfiguration tracerConfiguration, Resource libraryResource) 
            : this(spanProcessor, tracerConfiguration, new BinaryFormat(), new TraceContextFormat(), libraryResource)
        {
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
            this.ActiveTracerConfiguration = tracerConfiguration ?? throw new ArgumentNullException(nameof(tracerConfiguration));
            this.BinaryFormat = binaryFormat ?? throw new ArgumentNullException(nameof(binaryFormat));
            this.TextFormat = textFormat ?? throw new ArgumentNullException(nameof(textFormat));
            this.LibraryResource = libraryResource ?? throw new ArgumentNullException(nameof(libraryResource));
        }

        public Resource LibraryResource { get; }

        /// <inheritdoc/>
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat { get; }

        /// <inheritdoc/>
        public ITextFormat TextFormat { get; }

        public TracerConfiguration ActiveTracerConfiguration { get; set; }

        public IDisposable WithSpan(ISpan span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return CurrentSpanUtils.WithSpan(span, true);
        }

        /// <inheritdoc/>
        public ISpan CreateRootSpan(string operationName)
        {
            return this.CreateRootSpan(operationName, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        public ISpan CreateRootSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return this.CreateRootSpan(operationName, kind, startTimestamp, null);
        }

        /// <inheritdoc/>
        public ISpan CreateRootSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (startTimestamp == default)
            {
                startTimestamp = PreciseTimestamp.GetUtcNow();
            }

            return new Span(operationName, kind, startTimestamp, links, this.ActiveTracerConfiguration, this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName)
        {
            return this.CreateSpan(operationName, this.CurrentSpan, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return this.CreateSpan(operationName, null, kind, startTimestamp, null);
        }

        public ISpan CreateSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            return this.CreateSpan(operationName, null, kind, startTimestamp, links);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, ISpan parent)
        {
            return this.CreateSpan(operationName, parent, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind)
        {
            return this.CreateSpan(operationName, parent, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return this.CreateSpan(operationName, parent, SpanKind.Internal, startTimestamp, null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (parent == null)
            {
                parent = this.CurrentSpan;
            }

            if (startTimestamp == default)
            {
                startTimestamp = PreciseTimestamp.GetUtcNow();
            }

            if (parent != null)
            {
                return new Span(operationName, parent, kind, startTimestamp, links, this.ActiveTracerConfiguration,
                    this.spanProcessor, this.LibraryResource);
            }

            return new Span(operationName, kind, startTimestamp, links, this.ActiveTracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, in SpanContext parent)
        {
            return this.CreateSpan(operationName, parent, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }
        
        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind)
        {
            return this.CreateSpan(operationName, parent, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return this.CreateSpan(operationName, parent, kind, startTimestamp, null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpan(string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (startTimestamp == default)
            {
                startTimestamp = PreciseTimestamp.GetUtcNow();
            }

            if (parent != null)
            {
                return new Span(operationName, parent, kind, startTimestamp, links, this.ActiveTracerConfiguration,
                    this.spanProcessor, this.LibraryResource);
            }

            return new Span(operationName, kind, startTimestamp, links, this.ActiveTracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan CreateSpanFromActivity(string operationName, Activity activity)
        {
            return this.CreateSpanFromActivity(operationName, activity, SpanKind.Internal, null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpanFromActivity(string operationName, Activity activity, SpanKind kind)
        {
            return this.CreateSpanFromActivity(operationName, activity, kind, null);
        }

        /// <inheritdoc/>
        public ISpan CreateSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (activity == null)
            {
                throw new ArgumentException("Current Activity cannot be null");
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

            return new Span(operationName, activity, kind, links, this.ActiveTracerConfiguration, this.spanProcessor, this.LibraryResource);
        }
    }
}
