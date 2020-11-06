// <copyright file="Tracer.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Tracer is the class responsible for creating <see cref="TelemetrySpan"/>.
    /// </summary>
    /// <remarks>Tracer is a wrapper around <see cref="ActivitySource"/> class.</remarks>
    public class Tracer
    {
        internal readonly ActivitySource ActivitySource;

        internal Tracer(ActivitySource activitySource)
        {
            this.ActivitySource = activitySource;
        }

        /// <summary>
        /// Gets the current span from the context.
        /// </summary>
        public static TelemetrySpan CurrentSpan
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return TelemetrySpan.NoopInstance;
                }
                else
                {
                    return new TelemetrySpan(currentActivity);
                }
            }
        }

        /// <summary>
        /// Makes the given span as the current one.
        /// </summary>
        /// <param name="span">The span to be made current.</param>
        /// <returns>The current span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TelemetrySpan WithSpan(TelemetrySpan span)
        {
            span?.Activate();
            return span;
        }

        /// <summary>
        /// Starts root span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="initialAttributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="Link"/> for the span.</param>
        /// <param name="startTime"> Start time for the span.</param>
        /// <returns>Span instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan StartRootSpan(string name, SpanKind kind = SpanKind.Internal, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            return this.StartSpanHelper(false, name, kind, default, initialAttributes, links, startTime);
        }

        /// <summary>
        /// Starts a span and does not make it as current span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parentSpan">Parent for new span.</param>
        /// <param name="initialAttributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="Link"/> for the span.</param>
        /// <param name="startTime"> Start time for the span.</param>
        /// <returns>Span instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public TelemetrySpan StartSpan(string name, SpanKind kind, in TelemetrySpan parentSpan, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            return this.StartSpan(name, kind, parentSpan?.Context ?? default, initialAttributes, links, startTime);
        }

        /// <summary>
        /// Starts a span and does not make it as current span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parentContext">Parent Context for new span.</param>
        /// <param name="initialAttributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="Link"/> for the span.</param>
        /// <param name="startTime"> Start time for the span.</param>
        /// <returns>Span instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public TelemetrySpan StartSpan(string name, SpanKind kind = SpanKind.Internal, in SpanContext parentContext = default, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            return this.StartSpanHelper(false, name, kind, parentContext, initialAttributes, links, startTime);
        }

        /// <summary>
        /// Starts a span and make it the current active span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parentSpan">Parent for new span.</param>
        /// <param name="initialAttributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="Link"/> for the span.</param>
        /// <param name="startTime"> Start time for the span.</param>
        /// <returns>Span instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public TelemetrySpan StartActiveSpan(string name, SpanKind kind, in TelemetrySpan parentSpan, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            return this.StartActiveSpan(name, kind, parentSpan?.Context ?? default, initialAttributes, links, startTime);
        }

        /// <summary>
        /// Starts a span and make it the current active span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parentContext">Parent Context for new span.</param>
        /// <param name="initialAttributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="Link"/> for the span.</param>
        /// <param name="startTime"> Start time for the span.</param>
        /// <returns>Span instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public TelemetrySpan StartActiveSpan(string name, SpanKind kind = SpanKind.Internal, in SpanContext parentContext = default, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            return this.StartSpanHelper(true, name, kind, parentContext, initialAttributes, links, startTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActivityKind ConvertToActivityKind(SpanKind kind)
        {
            return kind switch
            {
                SpanKind.Client => ActivityKind.Client,
                SpanKind.Consumer => ActivityKind.Consumer,
                SpanKind.Internal => ActivityKind.Internal,
                SpanKind.Producer => ActivityKind.Producer,
                SpanKind.Server => ActivityKind.Server,
                _ => ActivityKind.Internal,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TelemetrySpan StartSpanHelper(bool isActiveSpan, string name, SpanKind kind, in SpanContext parentContext = default, SpanAttributes initialAttributes = null, IEnumerable<Link> links = null, DateTimeOffset startTime = default)
        {
            if (!this.ActivitySource.HasListeners())
            {
                return TelemetrySpan.NoopInstance;
            }

            var activityKind = ConvertToActivityKind(kind);
            var activityLinks = links?.Select(l => l.ActivityLink);

            Activity previousActivity = null;
            if (!isActiveSpan)
            {
                previousActivity = Activity.Current;
            }

            var activity = this.ActivitySource.StartActivity(name, activityKind, parentContext.ActivityContext, initialAttributes?.Attributes ?? null, activityLinks, startTime);
            if (activity == null)
            {
                return TelemetrySpan.NoopInstance;
            }

            if (!isActiveSpan)
            {
                Activity.Current = previousActivity;
            }

            return new TelemetrySpan(activity);
        }
    }
}
