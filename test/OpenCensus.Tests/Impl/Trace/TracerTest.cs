// <copyright file="TracerTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using Moq;
    using OpenCensus.Trace.Config;
    using OpenCensus.Trace.Internal;
    using Xunit;

    public class TracerTest
    {
        private const string SPAN_NAME = "MySpanName";
        private IStartEndHandler startEndHandler;
        private ITraceConfig traceConfig;
        private Tracer tracer;


        public TracerTest()
        {
            startEndHandler = Mock.Of<IStartEndHandler>();
            traceConfig = Mock.Of<ITraceConfig>();
            tracer = new Tracer(new RandomGenerator(), startEndHandler, traceConfig);
        }

        [Fact]
        public void CreateSpanBuilder()
        {
            ISpanBuilder spanBuilder = tracer.SpanBuilderWithExplicitParent(SPAN_NAME, parent: BlankSpan.Instance);
            Assert.IsType<SpanBuilder>(spanBuilder);
        }

        [Fact]
        public void CreateSpanBuilderWithRemoteParet()
        {
            ISpanBuilder spanBuilder = tracer.SpanBuilderWithRemoteParent(SPAN_NAME, remoteParentSpanContext: SpanContext.Invalid);
            Assert.IsType<SpanBuilder>(spanBuilder);
        }
    }
}
