// <copyright file="ProbabilityActivitySamplerTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Samplers.Test
{
    public class ProbabilityActivitySamplerTest
    {
        private const string ActivityDisplayName = "MyActivityName";
        private const int NumSampleTries = 1000;
        private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;
        private readonly ActivityTraceId traceId;
        private readonly ActivityContext sampledActivityContext;
        private readonly ActivityContext notSampledActivityContext;
        private readonly ActivityLink sampledLink;

        public ProbabilityActivitySamplerTest()
        {
            this.traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            this.sampledActivityContext = new ActivityContext(this.traceId, parentSpanId, ActivityTraceFlags.Recorded);
            this.notSampledActivityContext = new ActivityContext(this.traceId, parentSpanId, ActivityTraceFlags.None);
            this.sampledLink = new ActivityLink(this.sampledActivityContext);
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeHighProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProbabilityActivitySampler(1.01));
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeLowProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProbabilityActivitySampler(-0.00001));
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_NotSampledParent()
        {
            var neverSample = new ProbabilityActivitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, this.notSampledActivityContext, null, 0.0);
            var alwaysSample = new ProbabilityActivitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, this.notSampledActivityContext, null, 1.0);
            var fiftyPercentSample = new ProbabilityActivitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, this.notSampledActivityContext, null, 0.5);
            var twentyPercentSample = new ProbabilityActivitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, this.notSampledActivityContext, null, 0.2);
            var twoThirdsSample = new ProbabilityActivitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, this.notSampledActivityContext, null, 2.0 / 3.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParent()
        {
            var neverSample = new ProbabilityActivitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, this.sampledActivityContext, null, 1.0);
            var alwaysSample = new ProbabilityActivitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, this.sampledActivityContext, null, 1.0);
            var fiftyPercentSample = new ProbabilityActivitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, this.sampledActivityContext, null, 1.0);
            var twentyPercentSample = new ProbabilityActivitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, this.sampledActivityContext, null, 1.0);
            var twoThirdsSample = new ProbabilityActivitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, this.sampledActivityContext, null, 1.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParentLink()
        {
            var neverSample = new ProbabilityActivitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, this.notSampledActivityContext, new List<ActivityLink>() { this.sampledLink }, 1.0);
            var alwaysSample = new ProbabilityActivitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, this.notSampledActivityContext, new List<ActivityLink>() { this.sampledLink }, 1.0);
            var fiftyPercentSample = new ProbabilityActivitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, this.notSampledActivityContext, new List<ActivityLink>() { this.sampledLink }, 1.0);
            var twentyPercentSample = new ProbabilityActivitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, this.notSampledActivityContext, new List<ActivityLink>() { this.sampledLink }, 1.0);
            var twoThirdsSample = new ProbabilityActivitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, this.notSampledActivityContext, new List<ActivityLink>() { this.sampledLink }, 1.0);
        }

        [Fact]
        public void ProbabilitySampler_SampleBasedOnTraceId()
        {
            ActivitySampler defaultProbability = new ProbabilityActivitySampler(0.0001);

            // This traceId will not be sampled by the ProbabilityActivitySampler because the first 8 bytes as long
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
            Assert.False(
                    defaultProbability.ShouldSample(new ActivitySamplingParameters(
                        default,
                        notSampledtraceId,
                        ActivityDisplayName,
                        ActivityKindServer,
                        null,
                        null)).IsSampled);

            // This traceId will be sampled by the ProbabilityActivitySampler because the first 8 bytes as long
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
            Assert.True(
                    defaultProbability.ShouldSample(new ActivitySamplingParameters(
                        default,
                        sampledtraceId,
                        ActivityDisplayName,
                        ActivityKindServer,
                        null,
                        null)).IsSampled);
        }

        [Fact]
        public void ProbabilitySampler_GetDescription()
        {
            var expectedDescription = "ProbabilityActivitySampler{0.500000}";
            Assert.Equal(expectedDescription, new ProbabilityActivitySampler(0.5).Description);
        }

        // Applies the given sampler to NumSampleTries random traceId/spanId pairs.
        private static void AssertSamplerSamplesWithProbability(
            ActivitySampler sampler, ActivityContext parent, List<ActivityLink> links, double probability)
        {
            var count = 0; // Count of spans with sampling enabled
            for (var i = 0; i < NumSampleTries; i++)
            {
                if (sampler.ShouldSample(new ActivitySamplingParameters(
                    parent,
                    ActivityTraceId.CreateRandom(),
                    ActivityDisplayName,
                    ActivityKindServer,
                    null,
                    links)).IsSampled)
                {
                    count++;
                }
            }

            var proportionSampled = (double)count / NumSampleTries;

            // Allow for a large amount of slop (+/- 10%) in number of sampled traces, to avoid flakiness.
            Assert.True(proportionSampled < probability + 0.1 && proportionSampled > probability - 0.1);
        }
    }
}
