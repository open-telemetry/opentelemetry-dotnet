// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class SamplerBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private ActivitySource? sourceNotModifyTracestate;
    private ActivitySource? sourceModifyTracestate;
    private ActivitySource? sourceAppendTracestate;
    private ActivityContext parentContext;
    private TracerProvider? tracerProviderNotModifyTracestate;
    private TracerProvider? tracerProviderModifyTracestate;
    private TracerProvider? tracerProviderAppendTracestate;
    private TraceIdRatioBasedSampler? ratioBasedSampler;
    private SamplingParameters samplingParameters;

    [GlobalSetup]
    public void Setup()
    {
        this.sourceNotModifyTracestate = new("SamplerNotModifyingTraceState");
        this.sourceModifyTracestate = new("SamplerModifyingTraceState");
        this.sourceAppendTracestate = new("SamplerAppendingTraceState");
        this.parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "a=b", true);
        this.ratioBasedSampler = new TraceIdRatioBasedSampler(0.5);
        this.samplingParameters = new SamplingParameters(
            default,
            ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
            "Benchmark",
            ActivityKind.Server,
            null,
            null);

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

        this.tracerProviderNotModifyTracestate = Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerNotModifyTracestate)
            .AddSource(this.sourceNotModifyTracestate.Name)
            .Build();

        this.tracerProviderModifyTracestate = Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerModifyTracestate)
            .AddSource(this.sourceModifyTracestate.Name)
            .Build();

        this.tracerProviderAppendTracestate = Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSamplerAppendTracestate)
            .AddSource(this.sourceAppendTracestate.Name)
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.sourceNotModifyTracestate?.Dispose();
        this.sourceModifyTracestate?.Dispose();
        this.sourceAppendTracestate?.Dispose();
        this.tracerProviderNotModifyTracestate?.Dispose();
        this.tracerProviderModifyTracestate?.Dispose();
        this.tracerProviderAppendTracestate?.Dispose();
    }

    [Benchmark]
    public SamplingResult TraceIdRatioBasedDecision()
        => this.ratioBasedSampler!.ShouldSample(in this.samplingParameters);

    [Benchmark]
    public void SamplerNotModifyingTraceState()
    {
        using var activity = this.sourceNotModifyTracestate!.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    [Benchmark]
    public void SamplerModifyingTraceState()
    {
        using var activity = this.sourceModifyTracestate!.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    [Benchmark]
    public void SamplerAppendingTraceState()
    {
        using var activity = this.sourceAppendTracestate!.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
    }

    internal sealed class TestSampler : Sampler
    {
        public Func<SamplingParameters, SamplingResult>? SamplingAction { get; set; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
