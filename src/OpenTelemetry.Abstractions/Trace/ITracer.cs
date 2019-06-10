// <copyright file="ITracer.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// Tracer to record distributed tracing informaiton.
    /// </summary>
    public interface ITracer
    {
        /// <summary>
        /// Gets the current span from the context.
        /// </summary>
        ISpan CurrentSpan { get; }

        /// <summary>
        /// Gets the <see cref="IBinaryFormat"/> for this implementation.
        /// </summary>
        IBinaryFormat BinaryFormat { get; }

        /// <summary>
        /// Gets the <see cref="ITextFormat"/> for this implementation.
        /// </summary>
        ITextFormat TextFormat { get; }

        /// <summary>
        /// Associates the span with the current context.
        /// </summary>
        /// <param name="span">Span to associate with the current context.</param>
        /// <returns>Scope object to control span to current context association.</returns>
        IScope WithSpan(ISpan span);

        /// <summary>
        /// Gets the span builder for the span with the given name.
        /// </summary>
        /// <param name="spanName">Span name.</param>
        /// <param name="spanKind">Span kind.</param>
        /// <returns>Span builder for the span with the given name.</returns>
        ISpanBuilder SpanBuilder(string spanName, SpanKind spanKind = SpanKind.Internal);

        /// <summary>
        /// Gets the span builder for the span with the given name and parent.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="parent">Parent of the span.</param>
        /// <returns>Span builder for the span with the given name and specified parent.</returns>
        ISpanBuilder SpanBuilderWithParent(string name, SpanKind kind = SpanKind.Internal, ISpan parent = null);

        /// <summary>
        /// Gets the span builder for the span with the give name and remote parent context.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="parentContext">Remote parent context extracted from the wire.</param>
        /// <returns>Span builder for the span with the given name and specified parent span context.</returns>
        ISpanBuilder SpanBuilderWithParentContext(string name, SpanKind kind = SpanKind.Internal, SpanContext parentContext = null);

        /// <summary>
        /// Records <see cref="SpanData"/>. This API allows to send a pre-populated span object to the
        /// exporter.Sampling and recording decisions as well as other collection optimizations is a
        /// responsibility of a caller.Note, the <see cref="SpanContext" /> object on the span population with
        /// the values that will allow correlation of telemetry is also a caller responsibility.
        /// </summary>
        /// <param name="span">Immutable Span Data to be reported to all exporters.</param>
        void RecordSpanData(SpanData span);
    }
}
