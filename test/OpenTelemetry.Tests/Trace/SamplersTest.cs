// <copyright file="SamplersTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class SamplersTest
    {
        private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;
        private readonly ActivityTraceId traceId;
        private readonly ActivitySpanId spanId;
        private readonly ActivitySpanId parentSpanId;

        public SamplersTest()
        {
            this.traceId = ActivityTraceId.CreateRandom();
            this.spanId = ActivitySpanId.CreateRandom();
            this.parentSpanId = ActivitySpanId.CreateRandom();
        }

        [Theory]
        [InlineData(ActivityTraceFlags.Recorded)]
        [InlineData(ActivityTraceFlags.None)]
        public void AlwaysOnSampler_AlwaysReturnTrue(ActivityTraceFlags flags)
        {
            var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
            var link = new ActivityLink(parentContext);

            Assert.Equal(
                SamplingDecision.RecordAndSampled,
                new AlwaysOnSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKindServer, null, new List<ActivityLink> { link })).Decision);
        }

        [Fact]
        public void AlwaysOnSampler_GetDescription()
        {
            Assert.Equal("AlwaysOnSampler", new AlwaysOnSampler().Description);
        }

        [Theory]
        [InlineData(ActivityTraceFlags.Recorded)]
        [InlineData(ActivityTraceFlags.None)]
        public void AlwaysOffSampler_AlwaysReturnFalse(ActivityTraceFlags flags)
        {
            var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
            var link = new ActivityLink(parentContext);

            Assert.Equal(
                SamplingDecision.NotRecord,
                new AlwaysOffSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKindServer, null, new List<ActivityLink> { link })).Decision);
        }

        [Fact]
        public void AlwaysOffSampler_GetDescription()
        {
            Assert.Equal("AlwaysOffSampler", new AlwaysOffSampler().Description);
        }
    }
}
