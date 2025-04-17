// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method          | Mean       | Error     | StdDev    | Gen0   | Allocated |
|---------------- |-----------:|----------:|----------:|-------:|----------:|
| NoListener      |   5.322 ns | 0.0314 ns | 0.0262 ns |      - |         - |
| OneProcessor    | 326.566 ns | 3.4034 ns | 3.0170 ns | 0.0701 |     440 B |
| TwoProcessors   | 335.646 ns | 3.8341 ns | 3.3988 ns | 0.0701 |     440 B |
| ThreeProcessors | 336.069 ns | 6.5628 ns | 8.5335 ns | 0.0701 |     440 B |
*/

namespace Benchmarks.Trace;

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
        // this activity won't be created as there is no listener
        using var activity = this.tracerWithNoListener.StartActiveSpan("Benchmark");
    }

    [Benchmark]
    public void OneProcessor()
    {
        using var activity = this.tracerWithOneProcessor.StartActiveSpan("Benchmark");
    }

    [Benchmark]
    public void TwoProcessors()
    {
        using var activity = this.tracerWithTwoProcessors.StartActiveSpan("Benchmark");
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        using var activity = this.tracerWithThreeProcessors.StartActiveSpan("Benchmark");
    }

    internal sealed class DummyActivityProcessor : BaseProcessor<Activity>;
}
