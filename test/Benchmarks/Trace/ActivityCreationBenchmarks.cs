// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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


| Method                                         | Mean      | Error    | StdDev   | Gen0   | Allocated |
|----------------------------------------------- |----------:|---------:|---------:|-------:|----------:|
| CreateActivity_NoopProcessor                   | 307.12 ns | 5.769 ns | 6.172 ns | 0.0663 |     416 B |
| CreateActivity_WithParentContext_NoopProcessor |  75.18 ns | 0.399 ns | 0.354 ns |      - |         - |
| CreateActivity_WithParentId_NoopProcessor      | 156.52 ns | 1.609 ns | 1.426 ns | 0.0229 |     144 B |
| CreateActivity_WithAttributes_NoopProcessor    | 372.34 ns | 6.215 ns | 4.852 ns | 0.0992 |     624 B |
| CreateActivity_WithKind_NoopProcessor          | 302.24 ns | 5.859 ns | 8.402 ns | 0.0663 |     416 B |
*/

namespace Benchmarks.Trace;

public class ActivityCreationBenchmarks
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
    public void CreateActivity_WithKind_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithKind(this.benchmarkSource);
}
