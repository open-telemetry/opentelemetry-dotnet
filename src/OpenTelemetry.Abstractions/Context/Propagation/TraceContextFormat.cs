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

namespace OpenTelemetry.Context.Propagation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Utils;

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

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string> { "tracestate", "traceparent" };

        /// <inheritdoc/>
        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            try
            {
                var traceparentCollection = getter(carrier, "traceparent").ToArray();
                var tracestateCollection = getter(carrier, "tracestate").ToArray();

                Activity activity = new Activity("TODO");
                if (traceparentCollection.Count() == 1)
                {
                    var traceparent = traceparentCollection?.FirstOrDefault();
                    if (traceparent != null)
                    {
                        activity.SetParentId(traceparent);
                    }
                }

                var tracestateResult = Tracestate.Empty;
                try
                {
                    // TODO tracestate on activity
                    var entries = new List<KeyValuePair<string, string>>();
                    var names = new HashSet<string>();
                    var discardTracestate = false;
                    if (tracestateCollection != null)
                    {
                        foreach (var tracestate in tracestateCollection)
                        {
                            if (string.IsNullOrWhiteSpace(tracestate))
                            {
                                continue;
                            }

                            // tracestate: rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01,congo=BleGNlZWRzIHRohbCBwbGVhc3VyZS4
                            var keyStartIdx = 0;
                            var length = tracestate.Length;
                            while (keyStartIdx < length)
                            {
                                // first skip any prefix commas and OWS
                                var c = tracestate[keyStartIdx];
                                while (c == ' ' || c == '\t' || c == ',')
                                {
                                    keyStartIdx++;
                                    if (keyStartIdx == length)
                                    {
                                        break;
                                    }

                                    c = tracestate[keyStartIdx];
                                }

                                if (keyStartIdx == length)
                                {
                                    break;
                                }

                                var keyEndIdx = tracestate.IndexOf("=", keyStartIdx);

                                if (keyEndIdx == -1)
                                {
                                    discardTracestate = true;
                                    break;
                                }

                                var valueStartIdx = keyEndIdx + 1;

                                var valueEndIdx = tracestate.IndexOf(",", valueStartIdx);
                                valueEndIdx = valueEndIdx == -1 ? length : valueEndIdx;

                                // this will throw for duplicated keys
                                var key = tracestate.Substring(keyStartIdx, keyEndIdx - keyStartIdx).TrimStart();
                                if (names.Add(key))
                                {
                                    entries.Add(
                                        new KeyValuePair<string, string>(
                                            key,
                                            tracestate.Substring(valueStartIdx, valueEndIdx - valueStartIdx).TrimEnd()));
                                }
                                else
                                {
                                    discardTracestate = true;
                                    break;
                                }

                                keyStartIdx = valueEndIdx + 1;
                            }
                        }
                    }

                    if (!discardTracestate)
                    {
                        var tracestateBuilder = Tracestate.Builder;

                        entries.Reverse();
                        foreach (var entry in entries)
                        {
                            tracestateBuilder.Set(entry.Key, entry.Value);
                        }

                        tracestateResult = tracestateBuilder.Build();
                    }
                }
                catch (Exception ex)
                {
                    // failure to parse tracestate should not disregard traceparent
                    // TODO: logging
                }

                return SpanContext.Create(activity.TraceId, activity.ParentSpanId, activity.ActivityTraceFlags, tracestateResult);
            }
            catch (Exception ex)
            {
                // TODO: logging
            }

            // in case of exception indicate to upstream that there is no parseable context from the top
            return null;
        }

        /// <inheritdoc/>
        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            // TODO extensions methods on trace flags to get string
            var traceparent = string.Concat("00-", spanContext.TraceId.ToHexString(), "-", spanContext.SpanId.ToHexString(), "-", (spanContext.TraceOptions & ActivityTraceFlags.Recorded) != 0 ? "01" : "00");

            setter(carrier, "traceparent", traceparent);

            var sb = new StringBuilder();
            var isFirst = true;

            foreach (var entry in spanContext.Tracestate.Entries)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    sb.Append(",");
                }

                sb.Append(entry.Key).Append("=").Append(entry.Value);
            }

            if (sb.Length > 0)
            {
                setter(carrier, "tracestate", sb.ToString());
            }
        }
    }
}
