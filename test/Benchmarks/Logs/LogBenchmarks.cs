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

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

/*
// * Summary *

BenchmarkDotNet = v0.13.1, OS = Windows 10.0.22000
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.101
  [Host]     : .NET 6.0.1(6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1(6.0.121.56705), X64 RyuJIT


|                                 Method |     Mean |    Error |   StdDev |   Median |  Gen 0 | Allocated |
|--------------------------------------- |---------:|---------:|---------:|---------:|-------:|----------:|
|                             NoListener | 135.6 ns | 10.90 ns | 32.15 ns | 125.7 ns | 0.0153 |      64 B |
|                           OneProcessor | 251.9 ns | 16.29 ns | 46.73 ns | 241.9 ns | 0.0553 |     232 B |
| OneProcessorWithLoggerMessageGenerator | 216.6 ns |  9.48 ns | 27.65 ns | 213.6 ns | 0.0401 |     168 B |
|                          TwoProcessors | 319.6 ns | 24.42 ns | 70.86 ns | 314.4 ns | 0.0553 |     232 B |
|                        ThreeProcessors | 289.2 ns | 21.75 ns | 62.76 ns | 274.4 ns | 0.0553 |     232 B |
*/

namespace Benchmarks.Logs
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
            this.loggerWithNoListener.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        [Benchmark]
        public void OneProcessor()
        {
            this.loggerWithOneProcessor.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        [Benchmark]
        public void OneProcessorWithLoggerMessageGenerator()
        {
            Food.SayHello(this.loggerWithOneProcessor, "tomato", 2.99);
        }

        [Benchmark]
        public void TwoProcessors()
        {
            this.loggerWithTwoProcessors.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        [Benchmark]
        public void ThreeProcessors()
        {
            this.loggerWithThreeProcessors.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        internal class DummyLogProcessor : BaseProcessor<LogRecord>
        {
        }
    }
}
