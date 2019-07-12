// <copyright file="SpanContext.cs" company="OpenTelemetry Authors">
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
    using System.Diagnostics;

    /// <summary>
    /// A class that represents a span context. A span context contains the state that must propagate to
    /// child <see cref="ISpan"/> and across process boundaries. It contains the identifiers <see cref="ActivityTraceId"/>
    /// and <see cref="ActivitySpanId"/> associated with the <see cref="ISpan"/> and a set of <see cref="TraceOptions"/>.
    /// </summary>
    public sealed class SpanContext
    {
        /// <summary>
        /// A blank <see cref="SpanContext"/> that can be used for no-op operations.
        /// </summary>
        public static readonly SpanContext Blank = new SpanContext(default(ActivityTraceId), default(ActivitySpanId), ActivityTraceFlags.None, Tracestate.Empty);

        private SpanContext(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags traceOptions, Tracestate tracestate)
        {
            this.TraceId = traceId;
            this.SpanId = spanId;
            this.TraceOptions = traceOptions;
            this.Tracestate = tracestate;
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
        /// Gets the <see cref="TraceOptions"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public ActivityTraceFlags TraceOptions { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SpanContext"/> is valid.
        /// </summary>
        public bool IsValid => this.IsTraceIdValid(this.TraceId) && this.IsSpanIdValid(this.SpanId);

        /// <summary>
        /// Gets the <see cref="Tracestate"/> associated with this <see cref="SpanContext"/>.
        /// </summary>
        public Tracestate Tracestate { get; }

        /// <summary>
        /// Creates a new <see cref="SpanContext"/> with the given identifiers and options.
        /// </summary>
        /// <param name="traceId">The <see cref="ActivityTraceId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="spanId">The <see cref="ActivitySpanId"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="traceOptions">The <see cref="TraceOptions"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <param name="tracestate">The <see cref="Tracestate"/> to associate with the <see cref="SpanContext"/>.</param>
        /// <returns>A new <see cref="SpanContext"/> with the given identifiers and options.</returns>
        public static SpanContext Create(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags traceOptions, Tracestate tracestate)
        {
            return new SpanContext(traceId, spanId, traceOptions, tracestate);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.TraceId.GetHashCode();
            result = (31 * result) + this.SpanId.GetHashCode();
            result = (31 * result) + this.TraceOptions.GetHashCode();
            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is SpanContext))
            {
                return false;
            }

            var that = (SpanContext)obj;
            return this.TraceId.Equals(that.TraceId)
                && this.SpanId.Equals(that.SpanId)
                && this.TraceOptions.Equals(that.TraceOptions);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "SpanContext{"
                   + "traceId=" + this.TraceId + ", "
                   + "spanId=" + this.SpanId + ", "
                   + "traceOptions=" + this.TraceOptions
                   + "}";
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
