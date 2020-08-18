﻿// <copyright file="B3Format.cs" company="OpenTelemetry Authors">
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
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// B3 text propagator. See https://github.com/openzipkin/b3-propagation for the specification.
    /// </summary>
    public sealed class B3Format : ITextFormat
    {
        internal const string XB3TraceId = "X-B3-TraceId";
        internal const string XB3SpanId = "X-B3-SpanId";
        internal const string XB3ParentSpanId = "X-B3-ParentSpanId";
        internal const string XB3Sampled = "X-B3-Sampled";
        internal const string XB3Flags = "X-B3-Flags";
        internal const string XB3Combined = "b3";
        internal const char XB3CombinedDelimiter = '-';

        // Used as the upper ActivityTraceId.SIZE hex characters of the traceID. B3-propagation used to send
        // ActivityTraceId.SIZE hex characters (8-bytes traceId) in the past.
        internal const string UpperTraceId = "0000000000000000";

        // Sampled value via the X_B3_SAMPLED header.
        internal const string SampledValue = "1";

        // "Debug" sampled value.
        internal const string FlagsValue = "1";

        private static readonly HashSet<string> AllFields = new HashSet<string>() { XB3TraceId, XB3SpanId, XB3ParentSpanId, XB3Sampled, XB3Flags };

        private readonly bool singleHeader;

        /// <summary>
        /// Initializes a new instance of the <see cref="B3Format"/> class.
        /// </summary>
        public B3Format()
            : this(false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="B3Format"/> class.
        /// </summary>
        /// <param name="singleHeader">Determines whether to use single or multiple headers when extracting or injecting span context.</param>
        public B3Format(bool singleHeader)
        {
            this.singleHeader = singleHeader;
        }

        /// <inheritdoc/>
        public ISet<string> Fields => AllFields;

        /// <inheritdoc/>
        public PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (context.ActivityContext.IsValid())
            {
                // If a valid context has already been extracted, perform a noop.
                return context;
            }

            if (carrier == null)
            {
                OpenTelemetrySdkEventSource.Log.FailedToExtractActivityContext(nameof(B3Format), "null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetrySdkEventSource.Log.FailedToExtractActivityContext(nameof(B3Format), "null getter");
                return context;
            }

            if (this.singleHeader)
            {
                return ExtractFromSingleHeader(context, carrier, getter);
            }
            else
            {
                return ExtractFromMultipleHeaders(context, carrier, getter);
            }
        }

        /// <inheritdoc/>
        public void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
            {
                OpenTelemetrySdkEventSource.Log.FailedToInjectActivityContext(nameof(B3Format), "invalid context");
                return;
            }

            if (carrier == null)
            {
                OpenTelemetrySdkEventSource.Log.FailedToInjectActivityContext(nameof(B3Format), "null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetrySdkEventSource.Log.FailedToInjectActivityContext(nameof(B3Format), "null setter");
                return;
            }

            if (this.singleHeader)
            {
                var sb = new StringBuilder();
                sb.Append(context.ActivityContext.TraceId.ToHexString());
                sb.Append(XB3CombinedDelimiter);
                sb.Append(context.ActivityContext.SpanId.ToHexString());
                if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
                {
                    sb.Append(XB3CombinedDelimiter);
                    sb.Append(SampledValue);
                }

                setter(carrier, XB3Combined, sb.ToString());
            }
            else
            {
                setter(carrier, XB3TraceId, context.ActivityContext.TraceId.ToHexString());
                setter(carrier, XB3SpanId, context.ActivityContext.SpanId.ToHexString());
                if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
                {
                    setter(carrier, XB3Sampled, SampledValue);
                }
            }
        }

        private static PropagationContext ExtractFromMultipleHeaders<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
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
                    return context;
                }

                ActivitySpanId spanId;
                var spanIdStr = getter(carrier, XB3SpanId)?.FirstOrDefault();
                if (spanIdStr != null)
                {
                    spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());
                }
                else
                {
                    return context;
                }

                var traceOptions = ActivityTraceFlags.None;
                if (SampledValue.Equals(getter(carrier, XB3Sampled)?.FirstOrDefault())
                    || FlagsValue.Equals(getter(carrier, XB3Flags)?.FirstOrDefault()))
                {
                    traceOptions |= ActivityTraceFlags.Recorded;
                }

                return new PropagationContext(
                    new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                    context.ActivityBaggage);
            }
            catch (Exception e)
            {
                OpenTelemetrySdkEventSource.Log.ActivityContextExtractException(nameof(B3Format), e);
                return context;
            }
        }

        private static PropagationContext ExtractFromSingleHeader<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            try
            {
                var header = getter(carrier, XB3Combined)?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(header))
                {
                    return context;
                }

                var parts = header.Split(XB3CombinedDelimiter);
                if (parts.Length < 2 || parts.Length > 4)
                {
                    return context;
                }

                var traceIdStr = parts[0];
                if (string.IsNullOrWhiteSpace(traceIdStr))
                {
                    return context;
                }

                if (traceIdStr.Length == 16)
                {
                    // This is an 8-byte traceID.
                    traceIdStr = UpperTraceId + traceIdStr;
                }

                var traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());

                var spanIdStr = parts[1];
                if (string.IsNullOrWhiteSpace(spanIdStr))
                {
                    return context;
                }

                var spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

                var traceOptions = ActivityTraceFlags.None;
                if (parts.Length > 2)
                {
                    var traceFlagsStr = parts[2];
                    if (SampledValue.Equals(traceFlagsStr)
                        || FlagsValue.Equals(traceFlagsStr))
                    {
                        traceOptions |= ActivityTraceFlags.Recorded;
                    }
                }

                return new PropagationContext(
                    new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                    context.ActivityBaggage);
            }
            catch (Exception e)
            {
                OpenTelemetrySdkEventSource.Log.ActivityContextExtractException(nameof(B3Format), e);
                return context;
            }
        }
    }
}
