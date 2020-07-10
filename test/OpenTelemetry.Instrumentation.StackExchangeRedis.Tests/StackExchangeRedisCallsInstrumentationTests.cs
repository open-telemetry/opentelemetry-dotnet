// <copyright file="StackExchangeRedisCallsInstrumentationTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Threading.Tasks;

using Moq;

using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

using StackExchange.Redis;
using StackExchange.Redis.Profiling;

using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests
{
    public class StackExchangeRedisCallsInstrumentationTests
    {
        [Fact]
        public async void ProfilerSessionUsesTheSameDefault()
        {
            // connect to the server
            var connection = ConnectionMultiplexer.Connect("localhost:6379");

            var spanProcessor = new Mock<ActivityProcessor>();
            using (OpenTelemetrySdk.EnableOpenTelemetry(
                   (builder) =>
                    builder
                        .AddRedisInstrumentation(connection)
                        .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
            {
            }

            using var instrumentation = new StackExchangeRedisCallsInstrumentation(connection, new StackExchangeRedisCallsInstrumentationOptions());
            var profilerFactory = instrumentation.GetProfilerSessionsFactory();
            var first = profilerFactory();
            var second = profilerFactory();
            ProfilingSession third = null;
            await Task.Delay(1).ContinueWith((t) => { third = profilerFactory(); });
            Assert.Equal(first, second);
            Assert.Equal(second, third);
        }
    }
}
