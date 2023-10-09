// <copyright file="OpenTelemetrySdkBenchmarksActivity.cs" company="OpenTelemetry Authors">
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
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet=v0.13.2, OS=Windows 10 (10.0.19044.2130/21H2/November2021Update)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.100-preview.7.22377.5
  [Host]     : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2


|                                                   Method |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|--------------------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
|                             CreateActivity_NoopProcessor | 457.9 ns | 1.39 ns | 1.23 ns | 0.0992 |     416 B |
|           CreateActivity_WithParentContext_NoopProcessor | 104.8 ns | 0.43 ns | 0.36 ns |      - |         - |
|                CreateActivity_WithParentId_NoopProcessor | 221.9 ns | 0.44 ns | 0.39 ns | 0.0343 |     144 B |
|              CreateActivity_WithAttributes_NoopProcessor | 541.4 ns | 3.32 ns | 2.94 ns | 0.1488 |     624 B |
|                    CreateActiviti_WithKind_NoopProcessor | 437.5 ns | 2.05 ns | 1.92 ns | 0.0992 |     416 B |
*/

namespace Benchmarks.Trace;

public class OpenTelemetrySdkBenchmarksActivity
{
    private readonly ActivitySource benchmarkSource = new("Benchmark");
    private readonly ActivityContext parentCtx = new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
    private readonly string parentId = $"00-{ActivityTraceId.CreateRandom()}.{ActivitySpanId.CreateRandom()}.00";
    private TracerProvider tracerProvider;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("BenchMark")
            .Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.tracerProvider.Dispose();
        this.benchmarkSource.Dispose();
    }

    [Benchmark]
    public void CreateActivity_NoopProcessor() => ActivityCreationScenarios.CreateActivity(this.benchmarkSource);

    [Benchmark]
    public void CreateActivity_WithParentContext_NoopProcessor() => ActivityCreationScenarios.CreateActivityFromParentContext(this.benchmarkSource, this.parentCtx);

    [Benchmark]
    public void CreateActivity_WithParentId_NoopProcessor() => ActivityCreationScenarios.CreateActivityFromParentId(this.benchmarkSource, this.parentId);

    [Benchmark]
    public void CreateActivity_WithAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAttributes(this.benchmarkSource);

    [Benchmark]
    public void CreateActiviti_WithKind_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithKind(this.benchmarkSource);
}
