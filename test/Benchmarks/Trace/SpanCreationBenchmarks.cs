// <copyright file="SpanCreationBenchmarks.cs" company="OpenTelemetry Authors">
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
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method                           | Mean       | Error     | StdDev     | Ratio | RatioSD | Gen0   | Allocated |
|--------------------------------- |-----------:|----------:|-----------:|------:|--------:|-------:|----------:|
| CreateSpan_Sampled               | 357.256 ns | 2.1430 ns |  1.7895 ns | 61.41 |    0.40 | 0.0701 |     440 B |
| CreateSpan_ParentContext         | 354.797 ns | 2.4225 ns |  2.2660 ns | 60.93 |    0.46 | 0.0787 |     496 B |
| CreateSpan_Attributes_Sampled    | 460.082 ns | 8.7219 ns | 16.3818 ns | 81.52 |    2.88 | 0.1135 |     712 B |
| CreateSpan_WithSpan              | 439.489 ns | 8.7722 ns | 21.3526 ns | 79.36 |    2.81 | 0.1030 |     648 B |
| CreateSpan_Active                | 348.698 ns | 4.3437 ns |  3.8506 ns | 59.98 |    0.64 | 0.0701 |     440 B |
| CreateSpan_Active_GetCurrent     | 357.866 ns | 7.1779 ns |  9.0777 ns | 62.41 |    1.51 | 0.0701 |     440 B |
| CreateSpan_Attributes_NotSampled | 360.546 ns | 3.6948 ns |  3.2753 ns | 61.96 |    0.63 | 0.0815 |     512 B |
| CreateSpan_Noop                  |   5.818 ns | 0.0248 ns |  0.0207 ns |  1.00 |    0.00 |      - |         - |
| CreateSpan_Attributes_Noop       |  15.953 ns | 0.3446 ns |  0.3539 ns |  2.75 |    0.07 | 0.0115 |      72 B |
| CreateSpan_Propagate_Noop        |  12.320 ns | 0.2486 ns |  0.2326 ns |  2.12 |    0.04 |      - |         - |
*/

namespace Benchmarks.Trace;

public class SpanCreationBenchmarks
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
