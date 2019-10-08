// <copyright file="TraceParamsTest.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace.Config.Test
{
    using System;
    using OpenTelemetry.Trace.Configuration;
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class TraceConfigTest
    {
        [Fact]
        public void DefaultTraceConfig()
        {
            var config = new TracerConfiguration();
            Assert.Equal(Samplers.AlwaysSample, config.Sampler);
            Assert.Equal(32, config.MaxNumberOfAttributes);
            Assert.Equal(128, config.MaxNumberOfEvents);
            Assert.Equal(32, config.MaxNumberOfLinks);
        }

        [Fact]
        public void UpdateTraceParams_NullSampler()
        {
            Assert.Throws<ArgumentNullException>(() => new TracerConfiguration(null));
        }

        [Fact]
        public void UpdateTraceParams_NonPositiveMaxNumberOfAttributes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TracerConfiguration(Samplers.AlwaysSample, 0 ,1, 1));
        }

        [Fact]
        public void UpdateTraceParams_NonPositiveMaxNumberOfEvents()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TracerConfiguration(Samplers.AlwaysSample, 1, 0, 1));
        }


        [Fact]
        public void updateTraceParams_NonPositiveMaxNumberOfLinks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TracerConfiguration(Samplers.AlwaysSample, 1, 1, 0));
        }

        [Fact]
        public void UpdateTraceParams_All()
        {
            var traceParams = new TracerConfiguration(Samplers.NeverSample, 8, 9, 11);

            Assert.Equal(Samplers.NeverSample, traceParams.Sampler);
            Assert.Equal(8, traceParams.MaxNumberOfAttributes);
            Assert.Equal(9, traceParams.MaxNumberOfEvents);
            Assert.Equal(11, traceParams.MaxNumberOfLinks);
        }
    }
}
