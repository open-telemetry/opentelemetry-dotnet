// <copyright file="ITracer.cs" company="OpenCensus Authors">
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
    using OpenCensus.Common;

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
        ISpanBuilder SpanBuilder(string spanName, SpanKind spanKind = SpanKind.Unspecified);

        /// <summary>
        /// Gets the span builder for the span with the given name and parent.
        /// </summary>
        /// <param name="spanName">Span name.</param>
        /// <param name="spanKind">Span kind.</param>
        /// <param name="parent">Parent of the span.</param>
        /// <returns>Span builder for the span with the given name and specified parent.</returns>
        ISpanBuilder SpanBuilderWithExplicitParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpan parent = null);

        /// <summary>
        /// Gets the span builder for the span with the give name and remote parent context.
        /// </summary>
        /// <param name="spanName">Span name.</param>
        /// <param name="spanKind">Span kind.</param>
        /// <param name="remoteParentSpanContext">Remote parent context extracted from the wire.</param>
        /// <returns>Span builder for the span with the given name and specified parent span context.</returns>
        ISpanBuilder SpanBuilderWithRemoteParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpanContext remoteParentSpanContext = null);
    }
}
