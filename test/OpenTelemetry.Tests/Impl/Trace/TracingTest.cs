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

namespace OpenTelemetry.Trace.Test
{
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using Xunit;

    public class TracingTest
    {
        [Fact]
        public void DefaultTracer()
        {
            Assert.Equal(typeof(Tracer), Tracing.Tracer.GetType());
        }

        [Fact]
        public void DefaultSpanProcessor()
        {
            Assert.Equal(typeof(BatchingSpanProcessor), Tracing.SpanProcessor.GetType());
        }


        [Fact]
        public void DefaultTraceConfig()
        {
            Assert.Same(TraceConfig.Default, Tracing.TraceConfig);
        }
    }
}
