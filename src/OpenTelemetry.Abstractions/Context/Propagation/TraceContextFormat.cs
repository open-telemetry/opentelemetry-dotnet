// <copyright file="TraceContextFormat.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Abstractions.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// W3C trace context text wire protocol formatter. See https://github.com/w3c/distributed-tracing/.
    /// </summary>
    public class TraceContextFormat : ITextFormat
    {
        private static readonly int VersionLength = "00".Length;
        private static readonly int VersionPrefixIdLength = "00-".Length;
        private static readonly int TraceIdLength = "0af7651916cd43dd8448eb211c80319c".Length;
        private static readonly int VersionAndTraceIdLength = "00-0af7651916cd43dd8448eb211c80319c-".Length;
        private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;
        private static readonly int VersionAndTraceIdAndSpanIdLength = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-".Length;
        private static readonly int OptionsLength = "00".Length;
        private static readonly int TraceparentLengthV0 = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00".Length;

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string> { "tracestate", "traceparent" };

        /// <inheritdoc/>
        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            try
            {
                var traceparentCollection = getter(carrier, "traceparent");
                var tracestateCollection = getter(carrier, "tracestate");

                if (traceparentCollection != null && traceparentCollection.Count() > 1)
                {
                    // multiple traceparent are not allowed
                    return SpanContext.Blank;
                }

                var traceparent = traceparentCollection?.First();
                var traceparentParsed = this.TryExtractTraceparent(traceparent, out var traceId, out var spanId, out var traceoptions);

                if (!traceparentParsed)
                {
                    return SpanContext.Blank;
                }

                List<KeyValuePair<string, string>> tracestate = null;
                if (tracestateCollection != null)
                {
                    this.TryExtractTracestate(tracestateCollection.ToArray(), out tracestate);
                }

                return new SpanContext(traceId, spanId, traceoptions, tracestate);
            }
            catch (Exception)
            {
                // TODO: logging
            }

            // in case of exception indicate to upstream that there is no parseable context from the top
            return SpanContext.Blank;
        }

        /// <inheritdoc/>
        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            var traceparent = string.Concat("00-", spanContext.TraceId.ToHexString(), "-", spanContext.SpanId.ToHexString());
            traceparent = string.Concat(traceparent, (spanContext.TraceOptions & ActivityTraceFlags.Recorded) != 0 ? "-01" : "-00");

            setter(carrier, "traceparent", traceparent);

            string tracestateStr = TracestateUtils.GetString(spanContext.Tracestate);
            if (tracestateStr.Length > 0)
            {
                setter(carrier, "tracestate", tracestateStr);
            }
        }

        private bool TryExtractTraceparent(string traceparent, out ActivityTraceId traceId, out ActivitySpanId spanId, out ActivityTraceFlags traceOptions)
        {
            // from https://github.com/w3c/distributed-tracing/blob/master/trace_context/HTTP_HEADER_FORMAT.md
            // traceparent: 00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01

            traceId = default;
            spanId = default;
            traceOptions = default;
            var bestAttempt = false;

            if (string.IsNullOrWhiteSpace(traceparent) || traceparent.Length < TraceparentLengthV0)
            {
                return false;
            }

            // if version does not end with delimiter
            if (traceparent[VersionPrefixIdLength - 1] != '-')
            {
                return false;
            }

            // or version is not a hex (will throw)
            var version0 = this.HexCharToByte(traceparent[0]);
            var version1 = this.HexCharToByte(traceparent[1]);

            if (version0 == 0xf && version1 == 0xf)
            {
                return false;
            }

            if (version0 > 0)
            {
                // expected version is 00
                // for higher versions - best attempt parsing of trace id, span id, etc.
                bestAttempt = true;
            }

            if (traceparent[VersionAndTraceIdLength - 1] != '-')
            {
                return false;
            }

            try
            {
                traceId = ActivityTraceId.CreateFromString(traceparent.AsSpan().Slice(VersionPrefixIdLength, TraceIdLength));
            }
            catch (ArgumentOutOfRangeException)
            {
                // it's ok to still parse tracestate
                return false;
            }

            if (traceparent[VersionAndTraceIdAndSpanIdLength - 1] != '-')
            {
                return false;
            }

            try
            {
                spanId = ActivitySpanId.CreateFromString(traceparent.AsSpan().Slice(VersionAndTraceIdLength, SpanIdLength));
            }
            catch (ArgumentOutOfRangeException)
            {
                // it's ok to still parse tracestate
                return false;
            }

            byte options0;
            byte options1;

            try
            {
                options0 = this.HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength]);
                options1 = this.HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength + 1]);
            }
            catch (ArgumentOutOfRangeException)
            {
                // it's ok to still parse tracestate
                return false;
            }

            if ((options1 & 1) == 1)
            {
                traceOptions |= ActivityTraceFlags.Recorded;
            }

            if ((!bestAttempt) && (traceparent.Length != VersionAndTraceIdAndSpanIdLength + OptionsLength))
            {
                return false;
            }

            if (bestAttempt)
            {
                if ((traceparent.Length > TraceparentLengthV0) && (traceparent[TraceparentLengthV0] != '-'))
                {
                    return false;
                }
            }

            return true;
        }

        private byte HexCharToByte(char c)
        {
            if ((c >= '0') && (c <= '9'))
            {
                return (byte)(c - '0');
            }

            if ((c >= 'a') && (c <= 'f'))
            {
                return (byte)(c - 'a' + 10);
            }

            if ((c >= 'A') && (c <= 'F'))
            {
                return (byte)(c - 'A' + 10);
            }

            throw new ArgumentOutOfRangeException(nameof(c), $"Invalid character: {c}.");
        }

        private bool TryExtractTracestate(string[] tracestateCollection, out List<KeyValuePair<string, string>> tracestateResult)
        {
            tracestateResult = null;

            try
            {
                if (tracestateCollection != null)
                {
                    tracestateResult = new List<KeyValuePair<string, string>>();

                    // Iterate in reverse order because when call builder set the elements is added in the
                    // front of the list.
                    for (int i = tracestateCollection.Length - 1; i >= 0; i--)
                    {
                        if (!TracestateUtils.AppendTracestate(tracestateCollection[i], tracestateResult))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                // failure to parse tracestate should not disregard traceparent
                // TODO: logging
            }

            return false;
        }
    }
}
