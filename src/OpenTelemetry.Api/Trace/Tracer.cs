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

using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Tracer to record distributed tracing information.
    /// </summary>
    public class Tracer
    {
        private ActivitySource activitySource;

        internal Tracer(ActivitySource activitySource)
        {
            this.activitySource = activitySource;
        }

        /// <summary>
        /// Gets the current span from the context.
        /// </summary>
        public TelemetrySpan CurrentSpan
        {
            get
            {
                return new TelemetrySpan(Activity.Current);
            }
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <returns>Span instance.</returns>
        public TelemetrySpan StartSpan(string name, SpanKind kind = SpanKind.Internal)
        {
            if (!this.activitySource.HasListeners())
            {
                return TelemetrySpan.NoOpInstance;
            }

            var activityKind = this.ConvertToActivityKind(kind);
            var activity = this.activitySource.StartActivity(name, activityKind);
            if (activity == null)
            {
                return TelemetrySpan.NoOpInstance;
            }

            return new TelemetrySpan(activity);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <returns>Span instance.</returns>
        public TelemetrySpan StartSpan(string name, SpanKind kind, in SpanContext parent)
        {
            if (!this.activitySource.HasListeners())
            {
                return TelemetrySpan.NoOpInstance;
            }

            var activityKind = this.ConvertToActivityKind(kind);
            var activity = this.activitySource.StartActivity(name, activityKind, parent.ActivityContext);
            if (activity == null)
            {
                return TelemetrySpan.NoOpInstance;
            }

            return new TelemetrySpan(activity);
        }

        /// <summary>
        /// Starts span.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="parent">Parent for new span.</param>
        /// <param name="attributes">Initial attributes for the span.</param>
        /// <param name="links"> <see cref="ActivityLink"/> for the span.</param>
        /// <returns>Span instance.</returns>
        public TelemetrySpan StartSpan(string name, SpanKind kind, in SpanContext parent, IEnumerable<KeyValuePair<string, string>> attributes = null, IEnumerable<ActivityLink> links = null)
        {
            if (!this.activitySource.HasListeners())
            {
                return TelemetrySpan.NoOpInstance;
            }

            var activityKind = this.ConvertToActivityKind(kind);
            var activity = this.activitySource.StartActivity(name, activityKind, parent.ActivityContext, attributes, links);
            if (activity == null)
            {
                return TelemetrySpan.NoOpInstance;
            }

            return new TelemetrySpan(activity);
        }

        private ActivityKind ConvertToActivityKind(SpanKind kind)
        {
            switch (kind)
            {
                case SpanKind.Client:
                    return ActivityKind.Client;
                case SpanKind.Consumer:
                    return ActivityKind.Consumer;
                case SpanKind.Internal:
                    return ActivityKind.Internal;
                case SpanKind.Producer:
                    return ActivityKind.Producer;
                case SpanKind.Server:
                    return ActivityKind.Server;
                default:
                    return ActivityKind.Internal;
            }
        }
    }
}
