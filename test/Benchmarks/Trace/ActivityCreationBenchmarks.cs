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
| CreateActivity_NoopProcessor                   | 245.32 ns | 4.934 ns | 10.408 ns | 239.75 ns |
| CreateActivity_WithParentContext_NoopProcessor |  53.81 ns | 1.090 ns |  1.850 ns |  53.48 ns |
| CreateActivity_WithSetAttributes_NoopProcessor | 363.87 ns | 7.200 ns | 16.830 ns | 367.81 ns |
| CreateActivity_WithAddAttributes_NoopProcessor | 340.51 ns | 2.072 ns |  1.731 ns | 340.35 ns |
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
    public void CreateActivity_WithSetAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAttributes(this.benchmarkSource);

    [Benchmark]
    public void CreateActivity_WithAddAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAddAttributes(this.benchmarkSource);

    internal class NoopActivityProcessor : BaseProcessor<Activity>
    {
    }
}
