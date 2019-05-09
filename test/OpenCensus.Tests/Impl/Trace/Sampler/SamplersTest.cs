// <copyright file="SamplersTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Sampler.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using OpenCensus.Trace.Internal;
    using OpenCensus.Trace.Test;
    using Xunit;

    public class SamplersTest
    {
        private static readonly String SPAN_NAME = "MySpanName";
        private static readonly int NUM_SAMPLE_TRIES = 1000;
        private readonly IRandomGenerator random = new RandomGenerator(1234);
        private readonly ITraceId traceId;
        private readonly ISpanId parentSpanId;
        private readonly ISpanId spanId;
        private readonly ISpanContext sampledSpanContext;
        private readonly ISpanContext notSampledSpanContext;
        private readonly ISpan sampledSpan;

        public SamplersTest()
        {
            traceId = TraceId.GenerateRandomId(random);
            parentSpanId = SpanId.GenerateRandomId(random);
            spanId = SpanId.GenerateRandomId(random);
            sampledSpanContext = SpanContext.Create(traceId, parentSpanId, TraceOptions.Builder().SetIsSampled(true).Build(), Tracestate.Empty);
            notSampledSpanContext = SpanContext.Create(traceId, parentSpanId, TraceOptions.Default, Tracestate.Empty);
            sampledSpan = new NoopSpan(sampledSpanContext, SpanOptions.RecordEvents);
        }

        [Fact]
        public void AlwaysSampleSampler_AlwaysReturnTrue()
        {
            // Sampled parent.
            Assert.True(
                    Samplers.AlwaysSample
                        .ShouldSample(
                            sampledSpanContext,
                            false,
                            traceId,
                            spanId,
                            "Another name",
                            new List<ISpan>()));

            // Not sampled parent.
            Assert.True(
                    Samplers.AlwaysSample
                        .ShouldSample(
                            notSampledSpanContext,
                            false,
                            traceId,
                            spanId,
                            "Yet another name",
                            new List<ISpan>()));

        }

        [Fact]
        public void AlwaysSampleSampler_ToString()
        {
            Assert.Equal("AlwaysSampleSampler", Samplers.AlwaysSample.ToString());
        }

        [Fact]
        public void NeverSampleSampler_AlwaysReturnFalse()
        {
            // Sampled parent.
            Assert.False(
                    Samplers.NeverSample
                        .ShouldSample(
                            sampledSpanContext,
                            false,
                            traceId,
                            spanId,
                            "bar",
                            new List<ISpan>()));
            // Not sampled parent.
            Assert.False(
                    Samplers.NeverSample
                        .ShouldSample(
                            notSampledSpanContext,
                            false,
                            traceId,
                            spanId,
                            "quux",
                            new List<ISpan>()));
        }

        [Fact]
        public void NeverSampleSampler_ToString()
        {
            Assert.Equal("NeverSampleSampler", Samplers.NeverSample.ToString());
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeHighProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Samplers.GetProbabilitySampler(1.01));
        }

        [Fact]
        public void ProbabilitySampler_OutOfRangeLowProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Samplers.GetProbabilitySampler(-0.00001));
        }


        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_NotSampledParent()
        {
            ISampler neverSample = Samplers.GetProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, notSampledSpanContext, new List<ISpan>(), 0.0);
            ISampler alwaysSample = Samplers.GetProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, notSampledSpanContext, new List<ISpan>(), 1.0);
            ISampler fiftyPercentSample = Samplers.GetProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, notSampledSpanContext, new List<ISpan>(), 0.5);
            ISampler twentyPercentSample = Samplers.GetProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, notSampledSpanContext, new List<ISpan>(), 0.2);
            ISampler twoThirdsSample = Samplers.GetProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, notSampledSpanContext, new List<ISpan>(), 2.0 / 3.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParent()
        {
            ISampler neverSample = Samplers.GetProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, sampledSpanContext, new List<ISpan>(), 1.0);
            ISampler alwaysSample = Samplers.GetProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, sampledSpanContext, new List<ISpan>(), 1.0);
            ISampler fiftyPercentSample = Samplers.GetProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, sampledSpanContext, new List<ISpan>(), 1.0);
            ISampler twentyPercentSample = Samplers.GetProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, sampledSpanContext, new List<ISpan>(), 1.0);
            ISampler twoThirdsSample = Samplers.GetProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, sampledSpanContext, new List<ISpan>(), 1.0);
        }

        [Fact]
        public void ProbabilitySampler_DifferentProbabilities_SampledParentLink()
        {
            ISampler neverSample = Samplers.GetProbabilitySampler(0.0);
            AssertSamplerSamplesWithProbability(
                neverSample, notSampledSpanContext, new List<ISpan>() { sampledSpan }, 1.0);
            ISampler alwaysSample = Samplers.GetProbabilitySampler(1.0);
            AssertSamplerSamplesWithProbability(
                alwaysSample, notSampledSpanContext, new List<ISpan>() { sampledSpan }, 1.0);
            ISampler fiftyPercentSample = Samplers.GetProbabilitySampler(0.5);
            AssertSamplerSamplesWithProbability(
                fiftyPercentSample, notSampledSpanContext, new List<ISpan>() { sampledSpan }, 1.0);
            ISampler twentyPercentSample = Samplers.GetProbabilitySampler(0.2);
            AssertSamplerSamplesWithProbability(
                twentyPercentSample, notSampledSpanContext, new List<ISpan>() { sampledSpan }, 1.0);
            ISampler twoThirdsSample = Samplers.GetProbabilitySampler(2.0 / 3.0);
            AssertSamplerSamplesWithProbability(
                twoThirdsSample, notSampledSpanContext, new List<ISpan>() { sampledSpan }, 1.0);
        }

        [Fact]
        public void ProbabilitySampler_SampleBasedOnTraceId()
        {
            ISampler defaultProbability = Samplers.GetProbabilitySampler(0.0001);
            // This traceId will not be sampled by the ProbabilitySampler because the first 8 bytes as long
            // is not less than probability * Long.MAX_VALUE;
            ITraceId notSampledtraceId =
                TraceId.FromBytes(
                    new byte[] {
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
                        null,
                        false,
                        notSampledtraceId,
                        SpanId.GenerateRandomId(random),
                        SPAN_NAME,
                        new List<ISpan>()));
            // This traceId will be sampled by the ProbabilitySampler because the first 8 bytes as long
            // is less than probability * Long.MAX_VALUE;
            ITraceId sampledtraceId =
                TraceId.FromBytes(
                    new byte[] {
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
                        null,
                        false,
                        sampledtraceId,
                        SpanId.GenerateRandomId(random),
                        SPAN_NAME,
                        new List<ISpan>()));
        }

        [Fact]
        public void ProbabilitySampler_getDescription()
        {
            Assert.Equal(String.Format("ProbabilitySampler({0:F6})", 0.5), Samplers.GetProbabilitySampler(0.5).Description);
        }

        [Fact]
        public void ProbabilitySampler_ToString()
        {
            var result = Samplers.GetProbabilitySampler(0.5).ToString();
            Assert.Contains($"0{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}5", result);
        }

        // Applies the given sampler to NUM_SAMPLE_TRIES random traceId/spanId pairs.
        private static void AssertSamplerSamplesWithProbability(
            ISampler sampler, ISpanContext parent, IEnumerable<ISpan> parentLinks, double probability)
        {
            RandomGenerator random = new RandomGenerator(1234);
            int count = 0; // Count of spans with sampling enabled
            for (int i = 0; i < NUM_SAMPLE_TRIES; i++)
            {
                if (sampler.ShouldSample(
                    parent,
                    false,
                    TraceId.GenerateRandomId(random),
                    SpanId.GenerateRandomId(random),
                    SPAN_NAME,
                    parentLinks))
                {
                    count++;
                }
            }
            double proportionSampled = (double)count / NUM_SAMPLE_TRIES;
            // Allow for a large amount of slop (+/- 10%) in number of sampled traces, to avoid flakiness.
            Assert.True(proportionSampled < probability + 0.1 && proportionSampled > probability - 0.1);
        }
    }
}
