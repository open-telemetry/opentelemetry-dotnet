// <copyright file="TracerExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="Tracer"/>.
    /// </summary>
    public static class TracerExtensions
    {
        /// <summary>
        /// Starts root span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartRootSpan(this Tracer tracer, string operationName)
        {
            return tracer.StartRootSpan(operationName, SpanKind.Internal, null);
        }

        /// <summary>
        /// Starts root span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartRootSpan(this Tracer tracer, string operationName, SpanKind kind)
        {
            return tracer.StartRootSpan(operationName, kind, null);
        }

        /// <summary>
        /// Starts span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName)
        {
            return tracer.StartSpan(operationName, null, SpanKind.Internal, null);
        }

        /// <summary>
        /// Starts span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, SpanKind kind)
        {
            return tracer.StartSpan(operationName, null, kind, null);
        }

        /// <summary>
        /// Starts span. If there is active current span, it becomes a parent for returned span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="options">Advanced span creation options.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, SpanKind kind, SpanCreationOptions options)
        {
            return tracer.StartSpan(operationName, null, kind, options);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, TelemetrySpan parent)
        {
            return tracer.StartSpan(operationName, parent, SpanKind.Internal, null);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, TelemetrySpan parent, SpanKind kind)
        {
            return tracer.StartSpan(operationName, parent, kind, null);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, in SpanContext parent)
        {
            return tracer.StartSpan(operationName, parent, SpanKind.Internal, null);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public static TelemetrySpan StartSpan(this Tracer tracer, string operationName, in SpanContext parent, SpanKind kind)
        {
            return tracer.StartSpan(operationName, parent, kind, null);
        }

        /// <summary>
        /// Starts active span from auto-collected <see cref="Activity"/>.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="activity">Activity instance to create span from.</param>
        /// <returns>Span scope instance.</returns>
        public static TelemetrySpan StartSpanFromActivity(this Tracer tracer, string operationName, Activity activity)
        {
            return tracer.StartSpanFromActivity(operationName, activity, SpanKind.Internal, null);
        }

        /// <summary>
        /// Starts active span from auto-collected <see cref="Activity"/>.
        /// </summary>
        /// <param name="tracer">Tracer instance.</param>
        /// <param name="operationName">Span name.</param>
        /// <param name="activity">Activity instance to create span from.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span scope instance.</returns>
        public static TelemetrySpan StartSpanFromActivity(this Tracer tracer, string operationName, Activity activity, SpanKind kind)
        {
            return tracer.StartSpanFromActivity(operationName, activity, kind, null);
        }
    }
}
