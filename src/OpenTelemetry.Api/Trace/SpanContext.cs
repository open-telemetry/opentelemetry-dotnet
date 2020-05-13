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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A class that represents a span context. A span context contains the state that must propagate to
    /// child <see cref="TelemetrySpan"/> and across process boundaries. It contains the identifiers <see cref="ActivityTraceId"/>
    /// and <see cref="ActivitySpanId"/> associated with the <see cref="TelemetrySpan"/> and a set of <see cref="TraceFlags"/>.
    /// </summary>
    public readonly struct SpanContext
    {
        /// <summary>
        /// A blank <see cref="SpanContext"/> that can be used for remote no-op operations.
        /// </summary>
        internal static readonly SpanContext BlankRemote = new SpanContext(default, default, ActivityTraceFlags.None, true);

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContext"/> struct with the given identifiers and options.
        /// </summary>
        /// <param name="traceId">The <see cref="ActivityTraceId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="spanId">The <see cref="ActivitySpanId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="traceFlags">The <see cref="TraceFlags"/> to
        /// associate with the <see cref="SpanContext"/>.</param>
        /// <param name="isRemote">The value indicating whether this <see cref="SpanContext"/> was propagated from the remote parent.</param>
        /// <param name="tracestate">The tracestate to associate with the <see cref="SpanContext"/>.</param>
        public SpanContext(in ActivityTraceId traceId, in ActivitySpanId spanId, ActivityTraceFlags traceFlags, bool isRemote = false, IEnumerable<KeyValuePair<string, string>> tracestate = null)
        {
            this.TraceId = traceId;
            this.SpanId = spanId;
            this.TraceFlags = traceFlags;
            this.IsRemote = isRemote;
            this.Tracestate = tracestate ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Gets the <see cref="ActivityTraceId"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivityTraceId TraceId { get; }

        /// <summary>
        /// Gets the <see cref="ActivitySpanId"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivitySpanId SpanId { get; }

        /// <summary>
        /// Gets the <see cref="ActivityTraceFlags"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivityTraceFlags TraceFlags { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContext" />
        /// was propagated from a remote parent.
        /// </summary>
        public bool IsRemote { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContext"/> is valid.
        /// </summary>
        public bool IsValid => this.IsTraceIdValid(this.TraceId) && this.IsSpanIdValid(this.SpanId);

        /// <summary>
        /// Gets the <see cref="Tracestate"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Tracestate { get; }

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
            var result = 1;
            result = (31 * result) + this.TraceId.GetHashCode();
            result = (31 * result) + this.SpanId.GetHashCode();
            result = (31 * result) + this.TraceFlags.GetHashCode();
            result = (31 * result) + this.Tracestate.GetHashCode();
            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is SpanContext))
            {
                return false;
            }

            var that = (SpanContext)obj;

            return this.TraceId.Equals(that.TraceId)
                   && this.SpanId.Equals(that.SpanId)
                   && this.TraceFlags.Equals(that.TraceFlags)
                   && this.IsRemote == that.IsRemote
                   && ((this.Tracestate == null && that.Tracestate == null) || (this.Tracestate != null && this.Tracestate.Equals(that.Tracestate)));
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
