// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace;

public class OpenTelemetrySdkBenchmarks
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
