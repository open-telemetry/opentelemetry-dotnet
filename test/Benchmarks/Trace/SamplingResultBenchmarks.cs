// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
[MemoryDiagnoser]
public class SamplingResultBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private static readonly KeyValuePair<string, object>[] SamplingAttributes =
    [
        new("sampling.priority", 1),
        new("sampling.rule", "always"),
    ];

    private ActivitySource? sourceNoAttributes;
    private ActivitySource? sourceWithAttributeArray;
    private ActivitySource? sourceWithAttributeList;
    private ActivitySource? sourceDrop;
    private ActivitySource? sourceParentBased;

    private ActivityContext sampledRemoteParent;

    private TracerProvider? providerNoAttributes;
    private TracerProvider? providerWithAttributeArray;
    private TracerProvider? providerWithAttributeList;
    private TracerProvider? providerDrop;
    private TracerProvider? providerParentBased;

    [GlobalSetup]
    public void Setup()
    {
        this.sourceNoAttributes = new ActivitySource("SamplingResult.NoAttributes");
        this.sourceWithAttributeArray = new ActivitySource("SamplingResult.WithAttributeArray");
        this.sourceWithAttributeList = new ActivitySource("SamplingResult.WithAttributeList");
        this.sourceDrop = new ActivitySource("SamplingResult.Drop");
        this.sourceParentBased = new ActivitySource("SamplingResult.ParentBased");

        this.sampledRemoteParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: null,
            isRemote: true);

        // Sampler returns RecordAndSample with no attributes - the common case.
        this.providerNoAttributes = Sdk.CreateTracerProviderBuilder()
            .AddSource(this.sourceNoAttributes.Name)
            .SetSampler(new DelegateSampler(_ => new SamplingResult(SamplingDecision.RecordAndSample)))
            .Build();

        // Sampler returns attributes as a T[] - exercises the array fast-path.
        this.providerWithAttributeArray = Sdk.CreateTracerProviderBuilder()
            .AddSource(this.sourceWithAttributeArray.Name)
            .SetSampler(new DelegateSampler(_ => new SamplingResult(SamplingDecision.RecordAndSample, SamplingAttributes)))
            .Build();

        // Sampler returns attributes as a List<T> - exercises the IEnumerable fallback path.
        this.providerWithAttributeList = Sdk.CreateTracerProviderBuilder()
            .AddSource(this.sourceWithAttributeList.Name)
            .SetSampler(new DelegateSampler(_ => new SamplingResult(SamplingDecision.RecordAndSample, [.. SamplingAttributes])))
            .Build();

        // Sampler drops the span - attribute loop is never entered.
        this.providerDrop = Sdk.CreateTracerProviderBuilder()
            .AddSource(this.sourceDrop.Name)
            .SetSampler(new DelegateSampler(_ => new SamplingResult(SamplingDecision.Drop)))
            .Build();

        // ParentBasedSampler with AlwaysOnSampler root - realistic production default.
        this.providerParentBased = Sdk.CreateTracerProviderBuilder()
            .AddSource(this.sourceParentBased.Name)
            .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.sourceNoAttributes?.Dispose();
        this.sourceWithAttributeArray?.Dispose();
        this.sourceWithAttributeList?.Dispose();
        this.sourceDrop?.Dispose();
        this.sourceParentBased?.Dispose();

        this.providerNoAttributes?.Dispose();
        this.providerWithAttributeArray?.Dispose();
        this.providerWithAttributeList?.Dispose();
        this.providerDrop?.Dispose();
        this.providerParentBased?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void NoAttributes()
    {
        using var activity = this.sourceNoAttributes!.StartActivity("Benchmark", ActivityKind.Server, this.sampledRemoteParent);
    }

    [Benchmark]
    public void WithAttributeArray()
    {
        using var activity = this.sourceWithAttributeArray!.StartActivity("Benchmark", ActivityKind.Server, this.sampledRemoteParent);
    }

    [Benchmark]
    public void WithAttributeList()
    {
        using var activity = this.sourceWithAttributeList!.StartActivity("Benchmark", ActivityKind.Server, this.sampledRemoteParent);
    }

    [Benchmark]
    public void Drop()
    {
        using var activity = this.sourceDrop!.StartActivity("Benchmark", ActivityKind.Server, this.sampledRemoteParent);
    }

    [Benchmark]
    public void ParentBasedSampled()
    {
        using var activity = this.sourceParentBased!.StartActivity("Benchmark", ActivityKind.Server, this.sampledRemoteParent);
    }

    private sealed class DelegateSampler(Func<SamplingParameters, SamplingResult> sample) : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => sample(samplingParameters);
    }
}
