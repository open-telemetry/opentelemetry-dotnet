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
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class TracingTest
    {
        [Fact]
        public void DefaultTracerProvider()
        {
            Assert.Equal(typeof(TracerProviderBase), TracerProviderBase.Default.GetType());
            Assert.Equal(typeof(ProxyTracer), TracerProviderBase.Default.GetTracer(null).GetType());

            var tracerProvider = TracerProvider.Create(_ => { });
            TracerProviderBase.SetDefault(tracerProvider);
            Assert.IsAssignableFrom<TracerProvider>(TracerProviderBase.Default);
        }

        [Fact]
        public void DefaultTraceConfig()
        {
            var options = new TracerConfiguration();
            Assert.Equal(32, options.MaxNumberOfAttributes);
            Assert.Equal(128, options.MaxNumberOfEvents);
            Assert.Equal(32, options.MaxNumberOfLinks);
        }
    }
}
