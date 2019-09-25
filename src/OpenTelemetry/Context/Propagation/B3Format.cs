// <copyright file="B3Format.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Trace;

    /// <summary>
    /// B3 text propagator. See https://github.com/openzipkin/b3-propagation for the specification.
    /// </summary>
    public sealed class B3Format : ITextFormat
    {
        internal static readonly string XB3TraceId = "X-B3-TraceId";
        internal static readonly string XB3SpanId = "X-B3-SpanId";
        internal static readonly string XB3ParentSpanId = "X-B3-ParentSpanId";
        internal static readonly string XB3Sampled = "X-B3-Sampled";
        internal static readonly string XB3Flags = "X-B3-Flags";

        // Used as the upper ActivityTraceId.SIZE hex characters of the traceID. B3-propagation used to send
        // ActivityTraceId.SIZE hex characters (8-bytes traceId) in the past.
        internal static readonly string UpperTraceId = "0000000000000000";

        // Sampled value via the X_B3_SAMPLED header.
        internal static readonly string SampledValue = "1";

        // "Debug" sampled value.
        internal static readonly string FlagsValue = "1";

        private static readonly HashSet<string> AllFields = new HashSet<string>() { XB3TraceId, XB3SpanId, XB3ParentSpanId, XB3Sampled, XB3Flags };

        /// <inheritdoc/>
        public ISet<string> Fields
        {
            get
            {
                return AllFields;
            }
        }

        /// <inheritdoc/>
        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            if (getter == null)
            {
                throw new ArgumentNullException(nameof(getter));
            }

            try
            {
                ActivityTraceId traceId;
                var traceIdStr = getter(carrier, XB3TraceId)?.FirstOrDefault();
                if (traceIdStr != null)
                {
                    if (traceIdStr.Length == 16)
                    {
                        // This is an 8-byte traceID.
                        traceIdStr = UpperTraceId + traceIdStr;
                    }

                    traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());
                }
                else
                {
                    throw new SpanContextParseException("Missing X_B3_TRACE_ID.");
                }

                ActivitySpanId spanId;
                var spanIdStr = getter(carrier, XB3SpanId)?.FirstOrDefault();
                if (spanIdStr != null)
                {
                    spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());
                }
                else
                {
                    throw new SpanContextParseException("Missing X_B3_SPAN_ID.");
                }

                var traceOptions = ActivityTraceFlags.None;
                if (SampledValue.Equals(getter(carrier, XB3Sampled)?.FirstOrDefault())
                    || FlagsValue.Equals(getter(carrier, XB3Flags)?.FirstOrDefault()))
                {
                    traceOptions |= ActivityTraceFlags.Recorded;
                }

                return new SpanContext(traceId, spanId, traceOptions, Tracestate.Empty);
            }
            catch (Exception e)
            {
                throw new SpanContextParseException("Invalid input.", e);
            }
        }

        /// <inheritdoc/>
        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (!spanContext.IsValid)
            {
                throw new ArgumentException(nameof(spanContext));
            }

            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            if (setter == null)
            {
                throw new ArgumentNullException(nameof(setter));
            }

            setter(carrier, XB3TraceId, spanContext.TraceId.ToHexString());
            setter(carrier, XB3SpanId, spanContext.SpanId.ToHexString());
            if ((spanContext.TraceOptions & ActivityTraceFlags.Recorded) != 0)
            {
                setter(carrier, XB3Sampled, SampledValue);
            }
        }
    }
}
