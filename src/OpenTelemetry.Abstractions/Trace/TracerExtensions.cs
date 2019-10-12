// <copyright file="TracerExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Abstractions.Utils;

namespace OpenTelemetry.Trace
{
    public static class TracerExtensions
    {
        /// <summary>
        /// Creates root span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartRootSpan(this ITracer tracer, string operationName)
        {
            return tracer.StartRootSpan(operationName, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates root span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartRootSpan(this ITracer tracer, string operationName, SpanKind kind)
        {
            return tracer.StartRootSpan(operationName, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates root span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartRootSpan(this ITracer tracer, string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return tracer.StartRootSpan(operationName, kind, startTimestamp, null);
        }

        /// <summary>
        /// Creates span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName)
        {
            return tracer.StartSpan(operationName, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, SpanKind kind)
        {
            return tracer.StartSpan(operationName, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return tracer.StartSpan(operationName, kind, startTimestamp, null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, ISpan parent)
        {
            return tracer.StartSpan(operationName, parent, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, ISpan parent, SpanKind kind)
        {
            return tracer.StartSpan(operationName, parent, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return tracer.StartSpan(operationName, parent, kind, startTimestamp, null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, in SpanContext parent)
        {
            return tracer.StartSpan(operationName, parent, SpanKind.Internal, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, in SpanContext parent, SpanKind kind)
        {
            return tracer.StartSpan(operationName, parent, kind, PreciseTimestamp.GetUtcNow(), null);
        }

        /// <summary>
        /// Creates span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpan(this ITracer tracer, string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp)
        {
            return tracer.StartSpan(operationName, parent, kind, startTimestamp, null);
        }

        /// <summary>
        /// Creates span from auto-collected System.Diagnostics.Activity.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="activity">Activity instance to create span from.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpanFromActivity(this ITracer tracer, string operationName, Activity activity)
        {
            return tracer.StartSpanFromActivity(operationName, activity, SpanKind.Internal, null);
        }

        /// <summary>
        /// Creates span from auto-collected System.Diagnostics.Activity.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="activity">Activity instance to create span from.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static ISpan StartSpanFromActivity(this ITracer tracer, string operationName, Activity activity, SpanKind kind)
        {
            return tracer.StartSpanFromActivity(operationName, activity, kind, null);
        }
    }
}
