﻿// <copyright file="ITracer.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Tracer to record distributed tracing information.
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
        /// <returns>Disposable object to control span to current context association.</returns>
        IDisposable WithSpan(ISpan span);

        // TODO: add sampling hints

        /// <summary>
        /// Starts root span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts activate root span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        IDisposable StartActiveRootSpan(string operationName, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        ISpan StartSpan(string operationName, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts activate span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        IDisposable StartActiveSpan(string operationName, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts active span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        IDisposable StartActiveSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts active span.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span scope instance.</returns>
        IDisposable StartActiveSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options);

        /// <summary>
        /// Starts span from auto-collected <see cref="Activity"/>.
        /// </summary>
        /// <param name="operationName">Span name.</param>
        /// <param name="activity">Activity instance to create span from.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="links">Links collection.</param>
        /// <returns>Span scope instance.</returns>
        IDisposable StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links);
    }
}
