// <copyright file="SpanContextNew.cs" company="OpenTelemetry Authors">
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
    public readonly struct SpanContextNew
    {
        internal readonly ActivityContext ActivityContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextNew"/> struct with the given identifiers and options.
        /// </summary>
        /// <param name="traceId">The <see cref="ActivityTraceId"/> to associate with the <see cref="SpanContextNew"/>.</param>
        /// <param name="spanId">The <see cref="ActivitySpanId"/> to associate with the <see cref="SpanContextNew"/>.</param>
        /// <param name="traceFlags">The <see cref="TraceFlags"/> to
        /// associate with the <see cref="SpanContextNew"/>.</param>
        /// <param name="isRemote">The value indicating whether this <see cref="SpanContextNew"/> was propagated from the remote parent.</param>
        /// <param name="traceState">The traceState to associate with the <see cref="SpanContextNew"/>.</param>
        public SpanContextNew(in ActivityTraceId traceId, in ActivitySpanId spanId, ActivityTraceFlags traceFlags, bool isRemote = false, IEnumerable<KeyValuePair<string, string>> traceState = null)
        {
            this.ActivityContext = new ActivityContext(traceId, spanId, traceFlags, TracestateUtils.GetString(traceState));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextNew"/> struct with the given identifiers and options.
        /// </summary>
        /// <param name="activityContext">The activity context.</param>
        internal SpanContextNew(in ActivityContext activityContext)
        {
            this.ActivityContext = activityContext;
        }

        /// <summary>
        /// Gets the <see cref="ActivityTraceId"/> associated with this <see cref="SpanContextNew"/>.
        /// </summary>
        public ActivityTraceId TraceId
        {
            get
            {
                return this.ActivityContext.TraceId;
            }
        }

        /// <summary>
        /// Gets the <see cref="ActivitySpanId"/> associated with this <see cref="SpanContextNew"/>.
        /// </summary>
        public ActivitySpanId SpanId
        {
            get
            {
                return this.ActivityContext.SpanId;
            }
        }

        /// <summary>
        /// Gets the <see cref="ActivityTraceFlags"/> associated with this <see cref="SpanContextNew"/>.
        /// </summary>
        public ActivityTraceFlags TraceFlags
        {
            get
            {
                return this.ActivityContext.TraceFlags;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContextNew" />
        /// was propagated from a remote parent.
        /// </summary>
        public bool IsRemote
        {
            get
            {
                // TODO: return this.activityContext.IsRemote;
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContextNew"/> is valid.
        /// </summary>
        public bool IsValid => this.IsTraceIdValid(this.TraceId) && this.IsSpanIdValid(this.SpanId);

        /// <summary>
        /// Gets the <see cref="TraceState"/> associated with this <see cref="SpanContextNew"/>.
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
                TracestateUtils.AppendTracestate(this.ActivityContext.TraceState, traceStateResult);
                return traceStateResult;
            }
        }

        /// <summary>
        /// Compare two <see cref="SpanContextNew"/> for equality.
        /// </summary>
        /// <param name="spanContextNew1">First SpanContextNew to compare.</param>
        /// <param name="spanContextNew2">Second SpanContextNew to compare.</param>
        public static bool operator ==(SpanContextNew spanContextNew1, SpanContextNew spanContextNew2) => spanContextNew1.Equals(spanContextNew2);

        /// <summary>
        /// Compare two <see cref="SpanContextNew"/> for not equality.
        /// </summary>
        /// <param name="spanContextNew1">First SpanContextNew to compare.</param>
        /// <param name="spanContextNew2">Second SpanContextNew to compare.</param>
        public static bool operator !=(SpanContextNew spanContextNew1, SpanContextNew spanContextNew2) => !spanContextNew1.Equals(spanContextNew2);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.ActivityContext.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return this.ActivityContext.Equals(obj);
        }

        private bool IsTraceIdValid(ActivityTraceId traceId)
        {
            return traceId != default;
        }

        private bool IsSpanIdValid(ActivitySpanId spanId)
        {
            return spanId != default;
        }
    }
}
