// <copyright file="B3Format.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Propagation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// B3 text propagator. See https://github.com/openzipkin/b3-propagation for the specification.
    /// </summary>
    public sealed class B3Format : TextFormatBase
    {
        internal const string XB3TraceId = "X-B3-TraceId";
        internal const string XB3SpanId = "X-B3-SpanId";
        internal const string XB3ParentSpanId = "X-B3-ParentSpanId";
        internal const string XB3Sampled = "X-B3-Sampled";
        internal const string XB3Flags = "X-B3-Flags";

        // Used as the upper TraceId.SIZE hex characters of the traceID. B3-propagation used to send
        // TraceId.SIZE hex characters (8-bytes traceId) in the past.
        internal const string UpperTraceId = "0000000000000000";

        // Sampled value via the X_B3_SAMPLED header.
        internal const string SampledValue = "1";

        // "Debug" sampled value.
        internal const string FlagsValue = "1";

        private static readonly HashSet<string> AllFields = new HashSet<string>() { XB3TraceId, XB3SpanId, XB3ParentSpanId, XB3Sampled, XB3Flags };

        /// <inheritdoc/>
        public override ISet<string> Fields
        {
            get
            {
                return AllFields;
            }
        }

        /// <inheritdoc/>
        public override ISpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
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
                ITraceId traceId;
                string traceIdStr = getter(carrier, XB3TraceId)?.FirstOrDefault();
                if (traceIdStr != null)
                {
                    if (traceIdStr.Length == TraceId.Size)
                    {
                        // This is an 8-byte traceID.
                        traceIdStr = UpperTraceId + traceIdStr;
                    }

                    traceId = TraceId.FromLowerBase16(traceIdStr);
                }
                else
                {
                    throw new SpanContextParseException("Missing X_B3_TRACE_ID.");
                }

                ISpanId spanId;
                string spanIdStr = getter(carrier, XB3SpanId)?.FirstOrDefault();
                if (spanIdStr != null)
                {
                    spanId = SpanId.FromLowerBase16(spanIdStr);
                }
                else
                {
                    throw new SpanContextParseException("Missing X_B3_SPAN_ID.");
                }

                TraceOptions traceOptions = TraceOptions.Default;
                if (SampledValue.Equals(getter(carrier, XB3Sampled)?.FirstOrDefault())
                    || FlagsValue.Equals(getter(carrier, XB3Flags)?.FirstOrDefault()))
                {
                    traceOptions = TraceOptions.Builder().SetIsSampled(true).Build();
                }

                return SpanContext.Create(traceId, spanId, traceOptions, Tracestate.Empty);
            }
            catch (Exception e)
            {
                throw new SpanContextParseException("Invalid input.", e);
            }
        }

        /// <inheritdoc/>
        public override void Inject<T>(ISpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            if (setter == null)
            {
                throw new ArgumentNullException(nameof(setter));
            }

            setter(carrier, XB3TraceId, spanContext.TraceId.ToLowerBase16());
            setter(carrier, XB3SpanId, spanContext.SpanId.ToLowerBase16());
            if (spanContext.TraceOptions.IsSampled)
            {
                setter(carrier, XB3Sampled, SampledValue);
            }
        }
    }
}
