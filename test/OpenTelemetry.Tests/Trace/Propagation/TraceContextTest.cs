// <copyright file="TraceContextTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests
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
                return new[] { value };
            }

            return Empty;
        };

        private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
        {
            carrier[name] = value;
        };

        [Fact]
        public void TraceContextFormatCanParseExampleFromSpec()
        {
            var headers = new Dictionary<string, string>
            {
                { TraceParent, $"00-{TraceId}-{SpanId}-01" },
                { TraceState, $"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{TraceId}-00f067aa0ba902b7-01" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.ActivityContext.TraceId);
            Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.ActivityContext.SpanId);

            Assert.True(ctx.ActivityContext.IsRemote);
            Assert.True(ctx.ActivityContext.IsValid());
            Assert.True((ctx.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0);

            Assert.Equal($"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{TraceId}-00f067aa0ba902b7-01", ctx.ActivityContext.TraceState);
        }

        [Fact]
        public void TraceContextFormatNotSampled()
        {
            var headers = new Dictionary<string, string>
            {
                { TraceParent, $"00-{TraceId}-{SpanId}-00" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.ActivityContext.TraceId);
            Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.ActivityContext.SpanId);
            Assert.True((ctx.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) == 0);

            Assert.True(ctx.ActivityContext.IsRemote);
            Assert.True(ctx.ActivityContext.IsValid());
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfNoHeader()
        {
            var headers = new Dictionary<string, string>();

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.False(ctx.ActivityContext.IsValid());
        }

        [Fact]
        public void TraceContextFormat_IsBlankIfInvalid()
        {
            var headers = new Dictionary<string, string>
            {
                { TraceParent, $"00-xyz7651916cd43dd8448eb211c80319c-{SpanId}-01" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.False(ctx.ActivityContext.IsValid());
        }

        [Fact]
        public void TraceContextFormat_TracestateToStringEmpty()
        {
            var headers = new Dictionary<string, string>
            {
                { TraceParent, $"00-{TraceId}-{SpanId}-01" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.Null(ctx.ActivityContext.TraceState);
        }

        [Fact]
        public void TraceContextFormat_TracestateToString()
        {
            var headers = new Dictionary<string, string>
            {
                { TraceParent, $"00-{TraceId}-{SpanId}-01" },
                { TraceState, "k1=v1,k2=v2,k3=v3" },
            };

            var f = new TraceContextFormat();
            var ctx = f.Extract(default, headers, Getter);

            Assert.Equal("k1=v1,k2=v2,k3=v3", ctx.ActivityContext.TraceState);
        }

        [Fact]
        public void TraceContextFormat_Inject_NoTracestate()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var expectedHeaders = new Dictionary<string, string>
            {
                { TraceParent, $"00-{traceId}-{spanId}-01" },
            };

            var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, traceState: null);
            PropagationContext propagationContext = new PropagationContext(activityContext, default);
            var carrier = new Dictionary<string, string>();
            var f = new TraceContextFormat();
            f.Inject(propagationContext, carrier, Setter);

            Assert.Equal(expectedHeaders, carrier);
        }

        [Fact]
        public void TraceContextFormat_Inject_WithTracestate()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var expectedHeaders = new Dictionary<string, string>
            {
                { TraceParent, $"00-{traceId}-{spanId}-01" },
                { TraceState, $"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{traceId}-00f067aa0ba902b7-01" },
            };

            var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, expectedHeaders[TraceState]);
            PropagationContext propagationContext = new PropagationContext(activityContext, default);
            var carrier = new Dictionary<string, string>();
            var f = new TraceContextFormat();
            f.Inject(propagationContext, carrier, Setter);

            Assert.Equal(expectedHeaders, carrier);
        }
    }
}
