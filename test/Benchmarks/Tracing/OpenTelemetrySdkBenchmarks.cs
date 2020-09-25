// <copyright file="OpenTelemetrySdkBenchmarks.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Benchmarks
{
    [MemoryDiagnoser]
    public class OpenTelemetrySdkBenchmarks
    {
        private Tracer alwaysSampleTracer;
        private Tracer neverSampleTracer;
        private Tracer noopTracer;
        private TracerProvider tracerProviderAlwaysOnSample;
        private TracerProvider tracerProviderAlwaysOffSample;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.tracerProviderAlwaysOnSample = Sdk.CreateTracerProviderBuilder()
                .AddSource("AlwaysOnSample")
                .SetSampler(new AlwaysOnSampler())
                .Build();

            this.tracerProviderAlwaysOffSample = Sdk.CreateTracerProviderBuilder()
                .AddSource("AlwaysOffSample")
                .SetSampler(new AlwaysOffSampler())
                .Build();

            using var traceProviderNoop = Sdk.CreateTracerProviderBuilder().Build();

            this.alwaysSampleTracer = TracerProvider.Default.GetTracer("AlwaysOnSample");
            this.neverSampleTracer = TracerProvider.Default.GetTracer("AlwaysOffSample");
            this.noopTracer = TracerProvider.Default.GetTracer("Noop");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.tracerProviderAlwaysOffSample.Dispose();
            this.tracerProviderAlwaysOnSample.Dispose();
        }

        [Benchmark]
        public void CreateSpan_Sampled() => SpanCreationScenarios.CreateSpan(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_ParentContext() => SpanCreationScenarios.CreateSpan_ParentContext(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_Attributes_Sampled() => SpanCreationScenarios.CreateSpan_Attributes(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_WithSpan() => SpanCreationScenarios.CreateSpan_Propagate(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_Active() => SpanCreationScenarios.CreateSpan_Active(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_Active_GetCurrent() => SpanCreationScenarios.CreateSpan_Active_GetCurrent(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_Attributes_NotSampled() => SpanCreationScenarios.CreateSpan_Attributes(this.neverSampleTracer);

        [Benchmark(Baseline = true)]
        public void CreateSpan_Noop() => SpanCreationScenarios.CreateSpan(this.noopTracer);

        [Benchmark]
        public void CreateSpan_Attributes_Noop() => SpanCreationScenarios.CreateSpan_Attributes(this.noopTracer);

        [Benchmark]
        public void CreateSpan_Propagate_Noop() => SpanCreationScenarios.CreateSpan_Propagate(this.noopTracer);
    }
}
