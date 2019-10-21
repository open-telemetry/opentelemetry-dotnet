// <copyright file="StackExchangeRedisCallsCollectorTests.cs" company="OpenTelemetry Authors">
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

using Moq;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using StackExchange.Redis.Profiling;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Collector.StackExchangeRedis
{
    public class StackExchangeRedisCallsCollectorTests
    {
        [Fact]
        public async void ProfilerSessionUsesTheSameDefault()
        {
            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .SetProcessor(_ => spanProcessor.Object))
                .GetTracer(null);

            using (var collector = new StackExchangeRedisCallsCollector(tracer))
            {
                var profilerFactory = collector.GetProfilerSessionsFactory();
                var first = profilerFactory();
                var second = profilerFactory();

                ProfilingSession third = null;
                await Task.Delay(1).ContinueWith((t) => { third = profilerFactory(); });

                Assert.Equal(first, second);
                Assert.Equal(second, third);
            }
        }
    }
}
