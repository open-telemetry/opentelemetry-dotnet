// <copyright file="TraceContextTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Impl.Trace.Propagation
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Context.Propagation;
    using Xunit;

    public class TraceContextTest
    {
        [Fact]
        public void TraceContextFormatCanParseExampleFromSpec()
        {
            var headers = new Dictionary<string, string>()
            {
                {"traceparent", "00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01"},
                {
                    "tracestate",
                    "congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01"
                },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, (h, n) => new string[] {h[n]});

            Assert.Equal(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()), ctx.TraceId);
            Assert.Equal(ActivitySpanId.CreateFromString("b9c7c989f97918e1".AsSpan()), ctx.SpanId);
            Assert.True((ctx.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            Assert.Equal(2, ctx.Tracestate.Entries.Count());

            var first = ctx.Tracestate.Entries.First();

            Assert.Equal("congo", first.Key);
            Assert.Equal("lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4", first.Value);

            var last = ctx.Tracestate.Entries.Last();

            Assert.Equal("rojo", last.Key);
            Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01", last.Value);
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfNoHeader()
        {
            var headers = new Dictionary<string, string>();

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, (h, n) => new string[] { h[n] });

            Assert.Same(SpanContext.Blank, ctx);
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfInvalid()
        {
            var headers = new Dictionary<string, string>
            {
                {"traceparent", "00-xyz7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01"}
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, (h, n) => new string[] { h[n] });

            Assert.Same(SpanContext.Blank, ctx);
        }
    }
}
