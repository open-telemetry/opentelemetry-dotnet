// <copyright file="OpenTelemetrySdkBenchmarks.cs" company="OpenTelemetry Authors">
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

using BenchmarkDotNet.Attributes;
using Benchmarks.Tracing;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Samplers;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class OpenTelemetrySdkBenchmarks
    {
        private readonly Tracer alwaysSampleTracer;
        private readonly Tracer neverSampleTracer;
        private readonly Tracer noopTracer;

        public OpenTelemetrySdkBenchmarks()
        {
            this.alwaysSampleTracer = TracerFactory
                .Create(b => b.SetSampler(new AlwaysSampleSampler()))
                .GetTracer(null);
            this.neverSampleTracer = TracerFactory
                .Create(b => b.SetSampler(new NeverSampleSampler()))
                .GetTracer(null);
            this.noopTracer = TracerFactoryBase.Default.GetTracer(null);
        }

        [Benchmark]
        public TelemetrySpan CreateSpan_Sampled() => SpanCreationScenarios.CreateSpan(this.alwaysSampleTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_ParentContext() => SpanCreationScenarios.CreateSpan_ParentContext(this.alwaysSampleTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_Attributes_Sampled() => SpanCreationScenarios.CreateSpan_Attributes(this.alwaysSampleTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_WithSpan() => SpanCreationScenarios.CreateSpan_Propagate(this.alwaysSampleTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_Active() => SpanCreationScenarios.CreateSpan_Active(this.alwaysSampleTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_Active_GetCurrent() => SpanCreationScenarios.CreateSpan_Active_GetCurrent(this.alwaysSampleTracer);

        [Benchmark]
        public void CreateSpan_Attributes_NotSampled() => SpanCreationScenarios.CreateSpan_Attributes(this.neverSampleTracer);

        [Benchmark(Baseline = true)]
        public TelemetrySpan CreateSpan_Noop() => SpanCreationScenarios.CreateSpan(this.noopTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_Attributes_Noop() => SpanCreationScenarios.CreateSpan_Attributes(this.noopTracer);

        [Benchmark]
        public TelemetrySpan CreateSpan_Propagate_Noop() => SpanCreationScenarios.CreateSpan_Propagate(this.noopTracer);
    }
}
