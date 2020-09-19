// <copyright file="SpanContext.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Api.Context.Propagation;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A class that represents a span context. A span context contains the state that must propagate to
    /// child <see cref="TelemetrySpan"/> and across process boundaries. It contains the identifiers <see cref="ActivityTraceId"/>
    /// and <see cref="ActivitySpanId"/> associated with the <see cref="TelemetrySpan"/> and a set of <see cref="TraceFlags"/>.
    /// </summary>
    public readonly struct SpanContext : System.IEquatable<SpanContext>
    {
        internal readonly ActivityContext ActivityContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> struct with the given identifiers and options.
        /// </summary>
        /// <param name="traceId">The <see cref="ActivityTraceId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="spanId">The <see cref="ActivitySpanId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="traceFlags">The <see cref="TraceFlags"/> to
        /// associate with the <see cref="SpanContext"/>.</param>
        /// <param name="isRemote">The value indicating whether this <see cref="SpanContext"/> was propagated from the remote parent.</param>
        /// <param name="traceState">The traceState to associate with the <see cref="SpanContext"/>.</param>
        public SpanContext(in ActivityTraceId traceId, in ActivitySpanId spanId, ActivityTraceFlags traceFlags, bool isRemote = false, IEnumerable<KeyValuePair<string, string>> traceState = null)
        {
            this.ActivityContext = new ActivityContext(traceId, spanId, traceFlags, TraceStateUtilsNew.GetString(traceState), isRemote);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> struct with the given identifiers and options.
        /// </summary>
        /// <param name="activityContext">The activity context.</param>
        public SpanContext(in ActivityContext activityContext)
        {
            this.ActivityContext = activityContext;
        }

        /// <summary>
        /// Gets the <see cref="ActivityTraceId"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivityTraceId TraceId
        {
            get
            {
                return this.ActivityContext.TraceId;
            }
        }

        /// <summary>
        /// Gets the <see cref="ActivitySpanId"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivitySpanId SpanId
        {
            get
            {
                return this.ActivityContext.SpanId;
            }
        }

        /// <summary>
        /// Gets the <see cref="ActivityTraceFlags"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivityTraceFlags TraceFlags
        {
            get
            {
                return this.ActivityContext.TraceFlags;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContext" />
        /// was propagated from a remote parent.
        /// </summary>
        public bool IsRemote
        {
            get
            {
                return this.ActivityContext.IsRemote;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContext"/> is valid.
        /// </summary>
        public bool IsValid => IsTraceIdValid(this.TraceId) && IsSpanIdValid(this.SpanId);

        /// <summary>
        /// Gets the <see cref="TraceState"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> TraceState
        {
            get
            {
                if (string.IsNullOrEmpty(this.ActivityContext.TraceState))
                {
                    return Enumerable.Empty<KeyValuePair<string, string>>();
                }

                var traceStateResult = new List<KeyValuePair<string, string>>();
                TraceStateUtilsNew.AppendTraceState(this.ActivityContext.TraceState, traceStateResult);
                return traceStateResult;
            }
        }

        /// <summary>
        /// Converts a <see cref="SpanContext"/> into an <see cref="ActivityContext"/>.
        /// </summary>
        /// <param name="spanContext"><see cref="SpanContext"/> source.</param>
        public static implicit operator ActivityContext(SpanContext spanContext) => spanContext.ActivityContext;

        /// <summary>
        /// Compare two <see cref="SpanContext"/> for equality.
        /// </summary>
        /// <param name="spanContext1">First SpanContext to compare.</param>
        /// <param name="spanContext2">Second SpanContext to compare.</param>
        public static bool operator ==(SpanContext spanContext1, SpanContext spanContext2) => spanContext1.Equals(spanContext2);

        /// <summary>
        /// Compare two <see cref="SpanContext"/> for not equality.
        /// </summary>
        /// <param name="spanContext1">First SpanContext to compare.</param>
        /// <param name="spanContext2">Second SpanContext to compare.</param>
        public static bool operator !=(SpanContext spanContext1, SpanContext spanContext2) => !spanContext1.Equals(spanContext2);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.ActivityContext.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is SpanContext ctx) && this.ActivityContext.Equals(ctx.ActivityContext);
        }

        /// <inheritdoc/>
        public bool Equals(SpanContext other)
        {
            return this.ActivityContext.Equals(other.ActivityContext);
        }

        private static bool IsTraceIdValid(ActivityTraceId traceId)
        {
            return traceId != default;
        }

        private static bool IsSpanIdValid(ActivitySpanId spanId)
        {
            return spanId != default;
        }
    }
}
