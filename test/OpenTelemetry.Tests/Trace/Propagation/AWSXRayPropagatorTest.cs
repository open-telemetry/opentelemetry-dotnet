// <copyright file="AWSXRayPropagatorTest.cs" company="OpenTelemetry Authors">
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
    public class AWSXRayPropagatorTest
    {
        private const string AWSXRayTraceHeaderKey = "X-Amzn-Trace-Id";
        private const string TraceId = "5759e988bd862e3fe1be46a994272793";
        private const string ParentId = "53995c3f42cd8ad8";
        private const string TraceHeaderString = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1";
        private const string TraceHeaderStringNotSampled = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0";
        private const string TraceHeaderStringWithoutParentId = "Root=1-5759e988-bd862e3fe1be46a994272793;Sampled=1";
        private const string TraceHeaderStringWithoutSampleDecision = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8";
        private const string TraceHeaderStringWithInvalidTraceId = "Root=15759e988bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1";
        private const string TraceHeaderStringWithInvalidParentId = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=123;Sampled=1";
        private const string TraceHeaderStringWithInvalidSampleDecision = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=3";
        private const string TraceHeaderStringWithInvalidSampleDecisionLength = "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=123";

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

        private readonly AWSXRayPropagator awsXRayPropagator = new AWSXRayPropagator();

        [Fact]
        public void TestInjectTraceHeader()
        {
            var headers = new Dictionary<string, string>();
            var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
            var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
            var traceFlags = ActivityTraceFlags.Recorded;
            var activityContext = new ActivityContext(traceId, parentId, traceFlags);
            this.awsXRayPropagator.Inject(new PropagationContext(activityContext, default), headers, Setter);

            Assert.True(headers.ContainsKey(AWSXRayTraceHeaderKey));
            Assert.Equal(TraceHeaderString, headers[AWSXRayTraceHeaderKey]);
        }

        [Fact]
        public void TestExtractTraceHeader()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderString } };
            var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
            var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
            var traceFlags = ActivityTraceFlags.Recorded;
            var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

            Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderNotSampled()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringNotSampled } };
            var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
            var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
            var traceFlags = ActivityTraceFlags.None;
            var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

            Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithoutParentId()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithoutParentId } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithoutSampleDecision()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithoutSampleDecision } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithInvalidTraceId()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithInvalidTraceId } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithInvalidParentId()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithInvalidParentId } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithInvalidSampleDecision()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithInvalidSampleDecision } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }

        [Fact]
        public void TestExtractTraceHeaderWithInvalidSampleDecisionLength()
        {
            var headers = new Dictionary<string, string>() { { AWSXRayTraceHeaderKey, TraceHeaderStringWithInvalidSampleDecisionLength } };

            Assert.Equal(default, this.awsXRayPropagator.Extract(default, headers, Getter));
        }
    }
}
