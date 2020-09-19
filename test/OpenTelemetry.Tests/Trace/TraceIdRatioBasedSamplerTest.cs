// <copyright file="TraceIdRatioBasedSamplerTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TraceIdRatioBasedSamplerTest
    {
        private const string ActivityDisplayName = "MyActivityName";
        private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;

        [Fact]
        public void OutOfRangeHighProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(1.01));
        }

        [Fact]
        public void OutOfRangeLowProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(-0.00001));
        }

        [Fact]
        public void SampleBasedOnTraceId()
        {
            Sampler defaultProbability = new TraceIdRatioBasedSampler(0.0001);

            // This traceId will not be sampled by the TraceIdRatioBasedSampler because the first 8 bytes as long
            // is not less than probability * Long.MAX_VALUE;
            var notSampledtraceId =
                ActivityTraceId.CreateFromBytes(
                    new byte[]
                    {
                      0x8F,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                    });
            Assert.Equal(
                SamplingDecision.NotRecord,
                defaultProbability.ShouldSample(new SamplingParameters(default, notSampledtraceId, ActivityDisplayName, ActivityKindServer, null, null)).Decision);

            // This traceId will be sampled by the TraceIdRatioBasedSampler because the first 8 bytes as long
            // is less than probability * Long.MAX_VALUE;
            var sampledtraceId =
                ActivityTraceId.CreateFromBytes(
                    new byte[]
                    {
                      0x00,
                      0x00,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0xFF,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                      0,
                    });
            Assert.Equal(
                SamplingDecision.RecordAndSampled,
                defaultProbability.ShouldSample(new SamplingParameters(default, sampledtraceId, ActivityDisplayName, ActivityKindServer, null, null)).Decision);
        }

        [Fact]
        public void GetDescription()
        {
            var expectedDescription = "TraceIdRatioBasedSampler{0.500000}";
            Assert.Equal(expectedDescription, new TraceIdRatioBasedSampler(0.5).Description);
        }
    }
}
