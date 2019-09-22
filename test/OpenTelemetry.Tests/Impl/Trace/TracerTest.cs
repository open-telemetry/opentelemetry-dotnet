// <copyright file="TracerTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using Moq;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;

    using Xunit;

    public class TracerTest
    {
        private const string SpanName = "MySpanName";
        private readonly IStartEndHandler startEndHandler;
        private readonly TraceConfig traceConfig;
        private readonly Tracer tracer;


        public TracerTest()
        {
            startEndHandler = Mock.Of<IStartEndHandler>();
            traceConfig = TraceConfig.Default;
            tracer = new Tracer(startEndHandler, traceConfig);
        }

        [Fact]
        public void CreateSpanBuilder()
        {
            var spanBuilder = tracer.SpanBuilder(SpanName);
            Assert.IsType<SpanBuilder>(spanBuilder);
        }

        [Fact]
        public void CreateSpanBuilderWithNullName()
        {
            Assert.Throws<ArgumentNullException>(() => tracer.SpanBuilder(null));
        }

        [Fact]
        public void GetCurrentSpanBlank()
        {
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void GetCurrentSpan()
        {
            var span = tracer.SpanBuilder("foo").StartSpan();
            using (tracer.WithSpan(span))
            {
                Assert.Same(span, tracer.CurrentSpan);
            }
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void WithSpanNull()
        {
            Assert.Throws<ArgumentNullException>(() => tracer.WithSpan(null));
        }

        [Fact]
        public void GetTextFormat()
        {
            Assert.NotNull(tracer.TextFormat);
        }

        [Fact]
        public void GetBinaryFormat()
        {
            Assert.NotNull(tracer.BinaryFormat);
        }

        [Fact]
        public void GetActiveConfig()
        {
            var config = new TraceConfig(Samplers.NeverSample);
            var tracer = new Tracer(startEndHandler, config);
            Assert.Equal(config, tracer.ActiveTraceConfig);
        }

        [Fact]
        public void SetActiveConfig()
        {
            var config = new TraceConfig(Samplers.NeverSample);
            tracer.ActiveTraceConfig = config;
            Assert.Equal(config, tracer.ActiveTraceConfig);
        }

        // TODO test for sampler
    }
}
