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


| Method                        | Mean     | Error   | StdDev  | Gen0   | Allocated |
|------------------------------ |---------:|--------:|--------:|-------:|----------:|
| SamplerNotModifyingTraceState | 293.3 ns | 3.55 ns | 3.15 ns | 0.0520 |     328 B |
| SamplerModifyingTraceState    | 289.4 ns | 5.64 ns | 6.27 ns | 0.0520 |     328 B |
| SamplerAppendingTraceState    | 312.7 ns | 6.07 ns | 8.10 ns | 0.0610 |     384 B |
*/

namespace Benchmarks.Trace;

public class SamplerBenchmarks
{
    private readonly ActivitySource sourceNotModifyTracestate = new("SamplerNotModifyingTraceState");
    private readonly ActivitySource sourceModifyTracestate = new("SamplerModifyingTraceState");
    private readonly ActivitySource sourceAppendTracestate = new("SamplerAppendingTraceState");
    private readonly ActivityContext parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "a=b", true);

    public SamplerBenchmarks()
    {
        var testSamplerNotModifyTracestate = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                return new SamplingResult(SamplingDecision.RecordAndSample);
            },
        };

        var testSamplerModifyTracestate = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                return new SamplingResult(SamplingDecision.RecordAndSample, "a=b");
            },
        };

        var testSamplerAppendTracestate = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                return new SamplingResult(SamplingDecision.RecordAndSample, samplingParams.ParentContext.TraceState + ",addedkey=bar");
            },
        };

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerNotModifyTracestate)
            .AddSource(this.sourceNotModifyTracestate.Name)
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerModifyTracestate)
            .AddSource(this.sourceModifyTracestate.Name)
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerAppendTracestate)
            .AddSource(this.sourceAppendTracestate.Name)
            .Build();
    }

    [Benchmark]
    public void SamplerNotModifyingTraceState()
    {
        using var activity = this.sourceNotModifyTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    [Benchmark]
    public void SamplerModifyingTraceState()
    {
        using var activity = this.sourceModifyTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    [Benchmark]
    public void SamplerAppendingTraceState()
    {
        using var activity = this.sourceAppendTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    internal sealed class TestSampler : Sampler
    {
        public Func<SamplingParameters, SamplingResult>? SamplingAction { get; set; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(SamplingDecision.RecordAndSample);
        }
    }
}
