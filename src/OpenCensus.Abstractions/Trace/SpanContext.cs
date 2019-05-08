// <copyright file="SpanContext.cs" company="OpenCensus Authors">
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
    /// <summary>
    /// A class that represents a span context. A span context contains the state that must propagate to
    /// child <see cref="SpanBase"/> and across process boundaries. It contains the identifiers <see cref="TraceId"/>
    /// and <see cref="SpanId"/> associated with the <see cref="SpanBase"/> and a set of <see cref="TraceOptions"/>.
    /// </summary>
    public sealed class SpanContext : ISpanContext
    {
        public static readonly SpanContext Invalid = new SpanContext(Trace.TraceId.Invalid, Trace.SpanId.Invalid, TraceOptions.Default, Tracestate.Empty);

        private SpanContext(ITraceId traceId, ISpanId spanId, TraceOptions traceOptions, Tracestate tracestate)
        {
            this.TraceId = traceId;
            this.SpanId = spanId;
            this.TraceOptions = traceOptions;
            this.Tracestate = tracestate;
        }

        public ITraceId TraceId { get; }

        public ISpanId SpanId { get; }

        public TraceOptions TraceOptions { get; }

        public bool IsValid => this.TraceId.IsValid && this.SpanId.IsValid;

        public Tracestate Tracestate { get; }

        public static ISpanContext Create(ITraceId traceId, ISpanId spanId, TraceOptions traceOptions, Tracestate tracestate)
        {
            return new SpanContext(traceId, spanId, traceOptions, tracestate);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int result = 1;
            result = (31 * result) + (this.TraceId == null ? 0 : this.TraceId.GetHashCode());
            result = (31 * result) + (this.SpanId == null ? 0 : this.SpanId.GetHashCode());
            result = (31 * result) + (this.TraceOptions == null ? 0 : this.TraceOptions.GetHashCode());
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

            SpanContext that = (SpanContext)obj;
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
    }
}
