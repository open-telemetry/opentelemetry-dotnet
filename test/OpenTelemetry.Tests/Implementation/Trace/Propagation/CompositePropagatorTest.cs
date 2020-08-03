// <copyright file="CompositePropagatorTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace.Propagation
{
    public class CompositePropagatorTest
    {
        private const string TraceParent = "traceparent";
        private static readonly string[] Empty = new string[0];
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
        {
            count++;
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

        private static int count = 0;

        private readonly ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        private readonly ActivitySpanId spanId = ActivitySpanId.CreateRandom();

        [Fact]
        public void CompositePropagator_NullTextFormatList()
        {
            Assert.Throws<ArgumentNullException>(() => new CompositePropagator(null));
        }

        [Fact]
        public void CompositePropagator_WithTraceContext()
        {
            var expectedHeaders = new Dictionary<string, string>
            {
                { TraceParent, $"00-{this.traceId}-{this.spanId}-01" },
            };

            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TraceContextFormat(),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();
            compositePropagator.Inject(activityContext, carrier, Setter);

            Assert.Equal(expectedHeaders, carrier);

            var ctx = compositePropagator.Extract(activityContext, expectedHeaders, Getter);
            Assert.Equal(activityContext.TraceId, ctx.TraceId);
            Assert.Equal(activityContext.SpanId, ctx.SpanId);

            Assert.Empty(compositePropagator.Fields);
        }

        [Fact]
        public void CompositePropagator_WithTraceContextAndB3Format()
        {
            var expectedHeaders = new Dictionary<string, string>
            {
                { TraceParent, $"00-{this.traceId}-{this.spanId}-01" },
                { B3Format.XB3TraceId, this.traceId.ToString() },
                { B3Format.XB3SpanId, this.spanId.ToString() },
                { B3Format.XB3Sampled, "1" },
            };

            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TraceContextFormat(),
                new B3Format(),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();
            compositePropagator.Inject(activityContext, carrier, Setter);

            Assert.Equal(expectedHeaders, carrier);

            var ctx = compositePropagator.Extract(activityContext, expectedHeaders, Getter);
            Assert.Equal(activityContext.TraceId, ctx.TraceId);
            Assert.Equal(activityContext.SpanId, ctx.SpanId);
            Assert.True(ctx.IsValid());

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.True(isInjected);
        }

        [Fact]
        public void CompositePropagator_WithB3FormatAndTraceContext()
        {
            var expectedHeaders = new Dictionary<string, string>
            {
                { TraceParent, $"00-{this.traceId}-{this.spanId}-01" },
                { B3Format.XB3TraceId, this.traceId.ToString() },
                { B3Format.XB3SpanId, this.spanId.ToString() },
                { B3Format.XB3Sampled, "1" },
            };

            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new B3Format(),
                new TraceContextFormat(),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();
            compositePropagator.Inject(activityContext, carrier, Setter);

            Assert.Equal(expectedHeaders, carrier);

            var ctx = compositePropagator.Extract(activityContext, expectedHeaders, Getter);
            Assert.Equal(activityContext.TraceId, ctx.TraceId);
            Assert.Equal(activityContext.SpanId, ctx.SpanId);
            Assert.True(ctx.IsValid());

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.True(isInjected);
        }

        [Fact]
        public void CompositePropagator_B3FormatNotInjected()
        {
            var carrier = new Dictionary<string, string>
            {
                { TraceParent, $"00-{this.traceId}-{this.spanId}-01" },
            };

            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TraceContextFormat(),
                new B3Format(),
            });

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.False(isInjected);
        }

        [Fact]
        public void CompositePropagator_TestPropagator()
        {
            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TestPropagator("custom-traceparent-1", "custom-tracestate-1"),
                new TestPropagator("custom-traceparent-2", "custom-tracestate-2"),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();

            compositePropagator.Inject(activityContext, carrier, Setter);
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-1");
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-2");

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.True(isInjected);
        }

        [Fact]
        public void CompositePropagator_UsingSameTag()
        {
            const string header01 = "custom-tracestate-01";
            const string header02 = "custom-tracestate-02";

            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TestPropagator("custom-traceparent", header01, true),
                new TestPropagator("custom-traceparent", header02),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();

            compositePropagator.Inject(activityContext, carrier, Setter);
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent");

            // checking if the latest propagator is the one with the data. So, it will replace the previous one.
            Assert.Equal($"00-{this.traceId}-{this.spanId}-{header02.Split('-').Last()}", carrier["custom-traceparent"]);

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.True(isInjected);

            // resetting counter
            count = 0;
            ActivityContext newContext = compositePropagator.Extract(default, carrier, Getter);

            // checking if we accessed only two times: header/headerstate options
            // if that's true, we skipped the first one since we have a logic to for the default result
            Assert.Equal(2, count);
        }

        [Fact]
        public void CompositePropagator_CustomAndTraceFormats()
        {
            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TestPropagator("custom-traceparent", "custom-tracestate-1"),
                new TraceContextFormat(),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            var carrier = new Dictionary<string, string>();

            compositePropagator.Inject(activityContext, carrier, Setter);
            Assert.Equal(2, carrier.Count);

            bool isInjected = compositePropagator.IsInjected(carrier, Getter);
            Assert.True(isInjected);

            ActivityContext newContext = compositePropagator.Extract(default, carrier, Getter);
            Assert.Equal(this.traceId, newContext.TraceId);
            Assert.Equal(this.spanId, newContext.SpanId);
            Assert.True(newContext.IsValid());
        }
    }
}
