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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OpenTelemetry.Impl.Trace.Propagation
{
    public class TraceContextTest
    {
        private const string TraceParent = "traceparent";
        private const string TraceState = "tracestate";
        private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
        private const string SpanId = "b9c7c989f97918e1";

        private static readonly string[] Empty = new string[0];
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
        {
            if (headers.TryGetValue(name, out var value))
            {
                return new [] { value };
            }

            return Empty;
        };

        [Fact]
        public void TraceContextFormatCanParseExampleFromSpec()
        {
            var headers = new Dictionary<string, string>
            {
                {TraceParent, $"00-{TraceId}-{SpanId}-01"},
                {TraceState, $"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{TraceId}-00f067aa0ba902b7-01"},
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.TraceId);
            Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.SpanId);
            Assert.True(ctx.IsRemote);
            Assert.True(ctx.IsValid);
            Assert.True((ctx.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            Assert.Equal(2, ctx.Tracestate.Count());

            var first = ctx.Tracestate.First();

            Assert.Equal("congo", first.Key);
            Assert.Equal("lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4", first.Value);

            var last = ctx.Tracestate.Last();

            Assert.Equal("rojo", last.Key);
            Assert.Equal($"00-{TraceId}-00f067aa0ba902b7-01", last.Value);
        }

        [Fact]
        public void TraceContextFormatNotSampled()
        {
            var headers = new Dictionary<string, string>
            {
                {TraceParent, $"00-{TraceId}-{SpanId}-00"},
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.TraceId);
            Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.SpanId);
            Assert.True((ctx.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            Assert.True(ctx.IsRemote);
            Assert.True(ctx.IsValid);
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfNoHeader()
        {
            var headers = new Dictionary<string, string>();

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            Assert.False(ctx.IsValid);
            Assert.True(ctx.IsRemote);
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfInvalid()
        {
            var headers = new Dictionary<string, string>
            {
                {TraceParent, $"00-xyz7651916cd43dd8448eb211c80319c-{SpanId}-01"},
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            Assert.False(ctx.IsValid);
            Assert.True(ctx.IsRemote);
        }

        [Fact]
        public void TraceContextFormat_TracestateToStringEmpty()
        {
            var headers = new Dictionary<string, string>
            {
                {TraceParent, $"00-{TraceId}-{SpanId}-01"},
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            Assert.Empty(ctx.Tracestate);
            Assert.Equal(string.Empty, TracestateUtils.GetString(ctx.Tracestate));
        }

        [Fact]
        public void TraceContextFormat_TracestateToString()
        {
            var headers = new Dictionary<string, string>
            {
                {TraceParent, $"00-{TraceId}-{SpanId}-01"},
                {TraceState, "k1=v1,k2=v2,k3=v3" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(headers, Getter);

            var entries = ctx.Tracestate.ToArray();
            Assert.Equal(3, entries.Length);
            Assert.Equal("k1", entries[0].Key);
            Assert.Equal("v1", entries[0].Value);
            Assert.Equal("k2", entries[1].Key);
            Assert.Equal("v2", entries[1].Value);
            Assert.Equal("k3", entries[2].Key);
            Assert.Equal("v3", entries[2].Value);

            Assert.Equal("k1=v1,k2=v2,k3=v3", TracestateUtils.GetString(ctx.Tracestate));
        }
    }
}
