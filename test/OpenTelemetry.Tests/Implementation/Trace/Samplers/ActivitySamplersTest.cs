﻿// <copyright file="ActivitySamplersTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Samplers.Test
{
    public class ActivitySamplersTest
    {
        private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;
        private readonly ActivityTraceId traceId;
        private readonly ActivitySpanId spanId;
        private readonly ActivitySpanId parentSpanId;

        public ActivitySamplersTest()
        {
            traceId = ActivityTraceId.CreateRandom();
            spanId = ActivitySpanId.CreateRandom();
            parentSpanId = ActivitySpanId.CreateRandom();
        }

        [Theory]
        [InlineData(ActivityTraceFlags.Recorded)]
        [InlineData(ActivityTraceFlags.None)]
        public void AlwaysOnSampler_AlwaysReturnTrue(ActivityTraceFlags flags)
        {
            var parentContext = new ActivityContext(traceId, parentSpanId, flags);
            var link = new ActivityLink(parentContext);

            Assert.True(
                    new AlwaysOnActivitySampler()
                        .ShouldSample(
                            parentContext,
                            traceId,
                            spanId,
                            "Another name",
                            ActivityKindServer,
                            null,
                            new List<ActivityLink>() { link }).IsSampled);
        }

        [Fact]
        public void AlwaysOnSampler_GetDescription()
        {
            // TODO: The name must be AlwaysOnSampler as per spec.
            // We should correct it when we replace span sampler with this.
            Assert.Equal("AlwaysOnActivitySampler", new AlwaysOnActivitySampler().Description);
        }

        [Theory]
        [InlineData(ActivityTraceFlags.Recorded)]
        [InlineData(ActivityTraceFlags.None)]
        public void AlwaysOffSampler_AlwaysReturnFalse(ActivityTraceFlags flags)
        {
            var parentContext = new ActivityContext(traceId, parentSpanId, flags);
            var link = new ActivityLink(parentContext);

            Assert.False(
                    new AlwaysOffActivitySampler()
                        .ShouldSample(
                            parentContext,
                            traceId,
                            spanId,
                            "Another name",
                            ActivityKindServer,
                            null,
                            new List<ActivityLink>() { link }).IsSampled);
        }

        [Fact]
        public void AlwaysOffSampler_GetDescription()
        {
            // TODO: The name must be AlwaysOffSampler as per spec.
            // We should correct it when we replace span sampler with this.
            Assert.Equal("AlwaysOffActivitySampler", new AlwaysOffActivitySampler().Description);
        }
    }
}
