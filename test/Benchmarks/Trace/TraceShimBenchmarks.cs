// <copyright file="TraceShimBenchmarks.cs" company="OpenTelemetry Authors">
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
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class TraceShimBenchmarks
    {
        private readonly Tracer tracerWithNoListener = TracerProvider.Default.GetTracer("Benchmark.NoListener");
        private readonly Tracer tracerWithOneProcessor = TracerProvider.Default.GetTracer("Benchmark.OneProcessor");
        private readonly Tracer tracerWithTwoProcessors = TracerProvider.Default.GetTracer("Benchmark.TwoProcessors");
        private readonly Tracer tracerWithThreeProcessors = TracerProvider.Default.GetTracer("Benchmark.ThreeProcessors");

        public TraceShimBenchmarks()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("Benchmark.OneProcessor")
                .AddProcessor(new DummyActivityProcessor())
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("Benchmark.TwoProcessors")
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("Benchmark.ThreeProcessors")
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .Build();
        }

        [Benchmark]
        public void NoListener()
        {
            using (var activity = this.tracerWithNoListener.StartActiveSpan("Benchmark"))
            {
                // this activity won't be created as there is no listener
            }
        }

        [Benchmark]
        public void OneProcessor()
        {
            using (var activity = this.tracerWithOneProcessor.StartActiveSpan("Benchmark"))
            {
            }
        }

        [Benchmark]
        public void TwoProcessors()
        {
            using (var activity = this.tracerWithTwoProcessors.StartActiveSpan("Benchmark"))
            {
            }
        }

        [Benchmark]
        public void ThreeProcessors()
        {
            using (var activity = this.tracerWithThreeProcessors.StartActiveSpan("Benchmark"))
            {
            }
        }

        internal class DummyActivityProcessor : BaseProcessor<Activity>
        {
        }
    }
}
