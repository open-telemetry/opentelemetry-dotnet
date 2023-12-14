// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method                                         | Mean      | Error    | StdDev    | Median    |
|----------------------------------------------- |----------:|---------:|----------:|----------:|
| CreateActivity_NoopProcessor                   | 247.22 ns | 4.977 ns | 13.198 ns | 240.34 ns |
| CreateActivity_WithParentContext_NoopProcessor |  55.17 ns | 1.131 ns |  1.111 ns |  54.98 ns |
| CreateActivity_WithSetAttributes_NoopProcessor | 333.13 ns | 2.415 ns |  1.885 ns | 332.76 ns |
| CreateActivity_WithAddAttributes_NoopProcessor | 339.23 ns | 6.686 ns | 10.210 ns | 335.73 ns |
*/

namespace Benchmarks.Trace;

public class ActivityCreationBenchmarks
{
    private readonly ActivitySource benchmarkSource = new("Benchmark");
    private readonly ActivityContext parentCtx = new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
    private TracerProvider tracerProvider;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("BenchMark")
            .AddProcessor(new NoopActivityProcessor())
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
    public void CreateActivity_WithSetAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithSetAttributes(this.benchmarkSource);

    [Benchmark]
    public void CreateActivity_WithAddAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAddAttributes(this.benchmarkSource);

    internal class NoopActivityProcessor : BaseProcessor<Activity>
    {
    }
}
