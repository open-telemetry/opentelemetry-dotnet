// <copyright file="ParentOrElseSamplerTests.cs" company="OpenTelemetry Authors">
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
    public class ParentOrElseSamplerTests
    {
        private readonly ParentOrElseSampler parentOrElseAlwaysOnSampler = new ParentOrElseSampler(new AlwaysOnSampler());
        private readonly ParentOrElseSampler parentOrElseAlwaysOffSampler = new ParentOrElseSampler(new AlwaysOffSampler());

        [Fact]
        public void ParentOrElseSampler_SampledParent()
        {
            // No parent, use delegate sampler.
            Assert.Equal(
                new SamplingResult(true),
                this.parentOrElseAlwaysOnSampler.ShouldSample(default));

            // No parent, use delegate sampler.
            Assert.Equal(
                new SamplingResult(false),
                this.parentOrElseAlwaysOffSampler.ShouldSample(default));

            // Not sampled parent, don't sample.
            Assert.Equal(
                new SamplingResult(false),
                this.parentOrElseAlwaysOnSampler.ShouldSample(
                    new SamplingParameters(
                        parentContext: new ActivityContext(
                            ActivityTraceId.CreateRandom(),
                            ActivitySpanId.CreateRandom(),
                            ActivityTraceFlags.None),
                        traceId: default,
                        name: "Span",
                        kind: ActivityKind.Client)));

            // Sampled parent, sample.
            Assert.Equal(
                new SamplingResult(true),
                this.parentOrElseAlwaysOffSampler.ShouldSample(
                    new SamplingParameters(
                        parentContext: new ActivityContext(
                            ActivityTraceId.CreateRandom(),
                            ActivitySpanId.CreateRandom(),
                            ActivityTraceFlags.Recorded),
                        traceId: default,
                        name: "Span",
                        kind: ActivityKind.Client)));
        }

        [Fact]
        public void ParentOrElseSampler_SampledParentLink()
        {
            var notSampledLink = new ActivityLink[]
            {
                new ActivityLink(
                    new ActivityContext(
                        ActivityTraceId.CreateRandom(),
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.None)),
            };

            var sampledLink = new ActivityLink[]
            {
                new ActivityLink(
                    new ActivityContext(
                        ActivityTraceId.CreateRandom(),
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.Recorded)),
            };

            var notSampledParent = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            // Not sampled link, don't sample.
            Assert.Equal(
                new SamplingResult(false),
                this.parentOrElseAlwaysOnSampler.ShouldSample(
                    new SamplingParameters(
                        parentContext: notSampledParent,
                        traceId: default,
                        name: "Span",
                        kind: ActivityKind.Client,
                        links: notSampledLink)));

            // Sampled link, sample.
            Assert.Equal(
                new SamplingResult(true),
                this.parentOrElseAlwaysOffSampler.ShouldSample(
                    new SamplingParameters(
                        parentContext: notSampledParent,
                        traceId: default,
                        name: "Span",
                        kind: ActivityKind.Client,
                        links: sampledLink)));
        }
    }
}
