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
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Trace.Samplers.Test
{
    public class SamplersTest
    {
        private const string SpanName = "MySpanName";
        private const int NUM_SAMPLE_TRIES = 1000;
        private static readonly SpanKind SpanKindServer = SpanKind.Server;
        private readonly ActivityTraceId traceId;
        private readonly SpanContext sampledSpanContext;
        private readonly SpanContext notSampledSpanContext;
        private readonly Link sampledLink;

        public SamplersTest()
        {
            traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            sampledSpanContext = new SpanContext(traceId, parentSpanId, ActivityTraceFlags.Recorded);
            notSampledSpanContext = new SpanContext(traceId, parentSpanId, ActivityTraceFlags.None);
            sampledLink = new Link(sampledSpanContext);
        }

        [Fact]
        public void AlwaysOnSampler_AlwaysReturnTrue()
        {
            // Sampled parent.
            Assert.True(
                    new AlwaysOnSampler()
                        .ShouldSample(
                            sampledSpanContext,
                            traceId,
                            "Another name",
                            SpanKindServer,
                            null,
                            null).IsSampled);

            // Not sampled parent.
            Assert.True(
                    new AlwaysOnSampler()
                        .ShouldSample(
                            notSampledSpanContext,
                            traceId,
                            "Yet another name",
                            SpanKindServer,
                            null,
                            null).IsSampled);

        }

        [Fact]
        public void AlwaysOnSampler_GetDescription()
        {
            Assert.Equal("AlwaysOnSampler", new AlwaysOnSampler().Description);
        }

        [Fact]
        public void NeverOffSampler_AlwaysReturnFalse()
        {
            // Sampled parent.
            Assert.False(
                    new AlwaysOffSampler()
                        .ShouldSample(
                            sampledSpanContext,
                            traceId,
                            "bar",
                            SpanKindServer,
                            null,
                            null).IsSampled);
            // Not sampled parent.
            Assert.False(
                    new AlwaysOffSampler()
                        .ShouldSample(
                            notSampledSpanContext,
                            traceId,
                            "quux",
                            SpanKindServer,
                            null,
                            null).IsSampled);
        }

        [Fact]
        public void NeverSampleSampler_GetDescription()
        {
            Assert.Equal("AlwaysOffSampler", new AlwaysOffSampler().Description);
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeHighProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProbabilitySampler(1.01));
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeLowProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProbabilitySampler(-0.00001));
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_NotSampledParent()
        {
            Sampler neverSample = new ProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, notSampledSpanContext, null, 0.0);
            Sampler alwaysSample = new ProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, notSampledSpanContext, null, 1.0);
            Sampler fiftyPercentSample = new ProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, notSampledSpanContext, null, 0.5);
            Sampler twentyPercentSample = new ProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, notSampledSpanContext, null, 0.2);
            Sampler twoThirdsSample = new ProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, notSampledSpanContext, null, 2.0 / 3.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParent()
        {
            Sampler neverSample = new ProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, sampledSpanContext, null, 1.0);
            Sampler alwaysSample = new ProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, sampledSpanContext, null, 1.0);
            Sampler fiftyPercentSample = new ProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, sampledSpanContext, null, 1.0);
            Sampler twentyPercentSample = new ProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, sampledSpanContext, null, 1.0);
            Sampler twoThirdsSample = new ProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, sampledSpanContext, null, 1.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParentLink()
        {
            Sampler neverSample = new ProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, notSampledSpanContext, new List<Link>() { sampledLink }, 1.0);
            Sampler alwaysSample = new ProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, notSampledSpanContext, new List<Link>() { sampledLink }, 1.0);
            Sampler fiftyPercentSample = new ProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, notSampledSpanContext, new List<Link>() { sampledLink }, 1.0);
            Sampler twentyPercentSample = new ProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, notSampledSpanContext, new List<Link>() { sampledLink }, 1.0);
            Sampler twoThirdsSample = new ProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, notSampledSpanContext, new List<Link>() { sampledLink }, 1.0);
        }

        [Fact]
        public void ProbabilitySampler_SampleBasedOnTraceId()
        {
            Sampler defaultProbability = new ProbabilitySampler(0.0001);
            // This traceId will not be sampled by the ProbabilitySampler because the first 8 bytes as long
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
                    defaultProbability.ShouldSample(
                        default,
                        notSampledtraceId,
                        SpanName,
                        SpanKindServer,
                        null,
                        null).IsSampled);
            // This traceId will be sampled by the ProbabilitySampler because the first 8 bytes as long
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
                    defaultProbability.ShouldSample(
                        default,
                        sampledtraceId,
                        SpanName,
                        SpanKindServer,
                        null,
                        null).IsSampled);
        }

        [Fact]
        public void ProbabilitySampler_GetDescription()
        {
            var expectedDescription = "ProbabilitySampler{0.500000}";
            Assert.Equal(expectedDescription, new ProbabilitySampler(0.5).Description);
        }

        // Applies the given sampler to NUM_SAMPLE_TRIES random traceId/spanId pairs.
        private static void AssertSamplerSamplesWithProbability(
            Sampler sampler, SpanContext parent, List<Link> links, double probability)
        {
            var count = 0; // Count of spans with sampling enabled
            for (var i = 0; i < NUM_SAMPLE_TRIES; i++)
            {
                if (sampler.ShouldSample(
                    parent,
                    ActivityTraceId.CreateRandom(),
                    SpanName,
                    SpanKindServer,
                    null,
                    links).IsSampled)
                {
                    count++;
                }
            }
            var proportionSampled = (double)count / NUM_SAMPLE_TRIES;
            // Allow for a large amount of slop (+/- 10%) in number of sampled traces, to avoid flakiness.
            Assert.True(proportionSampled < probability + 0.1 && proportionSampled > probability - 0.1);
        }
    }
}
