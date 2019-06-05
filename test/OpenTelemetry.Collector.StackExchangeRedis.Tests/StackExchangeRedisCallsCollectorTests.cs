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

namespace OpenTelemetry.Collector.StackExchangeRedis
{
    using Moq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using StackExchange.Redis.Profiling;
    using System.Threading.Tasks;
    using Xunit;

    public class StackExchangeRedisCallsCollectorTests
    {
        [Fact]
        public async void ProfilerSessionUsesTheSameDefault()
        {
            var startEndHandler = new Mock<IStartEndHandler>();
            var tracer = new Tracer(new RandomGenerator(), startEndHandler.Object, new TraceConfig(), null);

            using (var collector = new StackExchangeRedisCallsCollector(null, tracer, null, null))
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
