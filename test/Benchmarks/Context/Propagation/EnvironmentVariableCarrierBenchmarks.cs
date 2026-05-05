// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Context.Propagation;

[MemoryDiagnoser]
public class EnvironmentVariableCarrierBenchmarks
{
    private static readonly TraceContextPropagator TraceContextPropagator = new();

    private static readonly CompositeTextMapPropagator CompositePropagator =
        new([new TraceContextPropagator(), new BaggagePropagator()]);

    [Params(false, true)]
    public bool IncludeBaggage { get; set; }

    public IReadOnlyDictionary<string, string?> ExtractCarrier { get; private set; } =
        new Dictionary<string, string?>();

    public Dictionary<string, string?> CaptureSource { get; private set; } = [];

    public Dictionary<string, string?> InjectCarrier { get; private set; } = [];

    public PropagationContext InjectContext { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
            ActivitySpanId.CreateFromString("b9c7c989f97918e1"),
            ActivityTraceFlags.Recorded,
            "key1=value1,key2=value2");

        var baggage = this.IncludeBaggage
            ? Baggage.Create(new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
                ["key3"] = "value3",
            })
            : default;

        this.CaptureSource = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01",
            ["tracestate"] = "key1=value1,key2=value2",
        };

        if (this.IncludeBaggage)
        {
            this.CaptureSource["baggage"] = "key1=value1,key2=value2,key3=value3";
        }

        this.ExtractCarrier = EnvironmentVariableCarrier.Capture(this.CaptureSource);
        this.InjectContext = new PropagationContext(activityContext, baggage);
        this.InjectCarrier = [];
    }

    [Benchmark]
    public IReadOnlyDictionary<string, string?> Capture() =>
        EnvironmentVariableCarrier.Capture(this.CaptureSource);

    [Benchmark]
    public PropagationContext Extract() =>
        this.IncludeBaggage
            ? CompositePropagator.Extract(default, this.ExtractCarrier, EnvironmentVariableCarrier.Get)
            : TraceContextPropagator.Extract(default, this.ExtractCarrier, EnvironmentVariableCarrier.Get);

    [Benchmark]
    public void Inject()
    {
        this.InjectCarrier.Clear();

        if (this.IncludeBaggage)
        {
            CompositePropagator.Inject(this.InjectContext, this.InjectCarrier, EnvironmentVariableCarrier.Set);
        }
        else
        {
            TraceContextPropagator.Inject(this.InjectContext, this.InjectCarrier, EnvironmentVariableCarrier.Set);
        }
    }
}
