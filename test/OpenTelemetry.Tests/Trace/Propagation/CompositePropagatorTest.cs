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
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class CompositePropagatorTest
    {
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
        public void CompositePropagator_TestPropagator()
        {
            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TestPropagator("custom-traceparent-1", "custom-tracestate-1"),
                new TestPropagator("custom-traceparent-2", "custom-tracestate-2"),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
            PropagationContext propagationContext = new PropagationContext(activityContext, null);
            var carrier = new Dictionary<string, string>();
            var activity = new Activity("test");

            compositePropagator.Inject(propagationContext, carrier, Setter);
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-1");
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-2");
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
            PropagationContext propagationContext = new PropagationContext(activityContext, null);

            var carrier = new Dictionary<string, string>();

            compositePropagator.Inject(propagationContext, carrier, Setter);
            Assert.Contains(carrier, kv => kv.Key == "custom-traceparent");

            // checking if the latest propagator is the one with the data. So, it will replace the previous one.
            Assert.Equal($"00-{this.traceId}-{this.spanId}-{header02.Split('-').Last()}", carrier["custom-traceparent"]);

            // resetting counter
            count = 0;
            compositePropagator.Extract(default, carrier, Getter);

            // checking if we accessed only two times: header/headerstate options
            // if that's true, we skipped the first one since we have a logic to for the default result
            Assert.Equal(2, count);
        }

        [Fact]
        public void CompositePropagator_ActivityContext_Baggage()
        {
            var compositePropagator = new CompositePropagator(new List<ITextFormat>
            {
                new TraceContextFormat(),
                new BaggageFormat(),
            });

            var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null, isRemote: true);
            var baggage = new Dictionary<string, string> { ["key1"] = "value1" };

            PropagationContext propagationContextActivityOnly = new PropagationContext(activityContext, null);
            PropagationContext propagationContextBaggageOnly = new PropagationContext(default, baggage);
            PropagationContext propagationContextBoth = new PropagationContext(activityContext, baggage);

            var carrier = new Dictionary<string, string>();
            compositePropagator.Inject(propagationContextActivityOnly, carrier, Setter);
            PropagationContext extractedContext = compositePropagator.Extract(default, carrier, Getter);
            Assert.Equal(propagationContextActivityOnly, extractedContext);

            carrier = new Dictionary<string, string>();
            compositePropagator.Inject(propagationContextBaggageOnly, carrier, Setter);
            extractedContext = compositePropagator.Extract(default, carrier, Getter);
            Assert.Equal(propagationContextBaggageOnly, extractedContext);

            carrier = new Dictionary<string, string>();
            compositePropagator.Inject(propagationContextBoth, carrier, Setter);
            extractedContext = compositePropagator.Extract(default, carrier, Getter);
            Assert.Equal(propagationContextBoth, extractedContext);
        }
    }
}
