// <copyright file="LogBenchmarks.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP3_1
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs.Benchmarks
{
    [MemoryDiagnoser]
    public class LogBenchmarks
    {
        private readonly ILogger loggerWithNoListener;
        private readonly ILogger loggerWithOneProcessor;
        private readonly ILogger loggerWithTwoProcessors;
        private readonly ILogger loggerWithThreeProcessors;

        public LogBenchmarks()
        {
            var loggerFactoryWithNoListener = LoggerFactory.Create(builder => { });
            this.loggerWithNoListener = loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();

            var loggerFactoryWithOneProcessor = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options => options
                    .AddProcessor(new DummyLogProcessor()));
            });
            this.loggerWithOneProcessor = loggerFactoryWithOneProcessor.CreateLogger<LogBenchmarks>();

            var loggerFactoryWithTwoProcessor = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options => options
                    .AddProcessor(new DummyLogProcessor())
                    .AddProcessor(new DummyLogProcessor()));
            });
            this.loggerWithTwoProcessors = loggerFactoryWithTwoProcessor.CreateLogger<LogBenchmarks>();

            var loggerFactoryWithThreeProcessor = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options => options
                    .AddProcessor(new DummyLogProcessor())
                    .AddProcessor(new DummyLogProcessor())
                    .AddProcessor(new DummyLogProcessor()));
            });
            this.loggerWithThreeProcessors = loggerFactoryWithThreeProcessor.CreateLogger<LogBenchmarks>();
        }

        [Benchmark]
        public void NoListener()
        {
            this.loggerWithNoListener.LogInformation("Hello, World!");
        }

        [Benchmark]
        public void OneProcessor()
        {
            this.loggerWithOneProcessor.LogInformation("Hello, World!");
        }

        [Benchmark]
        public void TwoProcessors()
        {
            this.loggerWithTwoProcessors.LogInformation("Hello, World!");
        }

        [Benchmark]
        public void ThreeProcessors()
        {
            this.loggerWithThreeProcessors.LogInformation("Hello, World!");
        }

        internal class DummyLogProcessor : BaseProcessor<LogRecord>
        {
        }
    }
}
#endif
