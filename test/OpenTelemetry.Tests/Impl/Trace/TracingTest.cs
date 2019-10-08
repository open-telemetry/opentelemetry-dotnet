// <copyright file="TracingTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Sampler.Internal;

namespace OpenTelemetry.Trace.Test
{
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using Xunit;

    public class TracingTest
    {
        [Fact]
        public void DefaultTracerFactory()
        {
            Assert.Equal(typeof(TracerFactory), Tracing.TracerFactory.GetType());
        }

        [Fact]
        public void DefaultSpanProcessor()
        {
            Assert.Equal(typeof(BatchingSpanProcessor), Tracing.SpanProcessor.GetType());
        }

        [Fact]
        public void DefaultTraceConfig()
        {
            Assert.IsType<AlwaysSampleSampler>(Tracing.TracerConfiguration.Sampler);
            Assert.Equal(32, Tracing.TracerConfiguration.MaxNumberOfAttributes);
            Assert.Equal(128, Tracing.TracerConfiguration.MaxNumberOfEvents);
            Assert.Equal(32, Tracing.TracerConfiguration.MaxNumberOfLinks);
        }
    }
}
