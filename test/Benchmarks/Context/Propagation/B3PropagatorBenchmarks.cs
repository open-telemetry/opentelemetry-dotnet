// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Context.Propagation;
using ApiB3Propagator = OpenTelemetry.Context.Propagation.B3Propagator;
using ExtensionsB3Propagator = OpenTelemetry.Extensions.Propagators.B3Propagator;

namespace Benchmarks.Context.Propagation;

[MemoryDiagnoser]
[Obsolete("Intentional coverage for obsolete API.")]
public class B3PropagatorBenchmarks
{
    private const string TraceIdBase16 = "ff000000000000000000000000000041";
    private const string SpanIdBase16 = "ff00000000000041";
    private const string B3TraceId = "X-B3-TraceId";
    private const string B3SpanId = "X-B3-SpanId";
    private const string B3Sampled = "X-B3-Sampled";
    private const string B3Combined = "b3";
    private const string SampledValue = "1";

    private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromString(TraceIdBase16.AsSpan());
    private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromString(SpanIdBase16.AsSpan());

    private static readonly ApiB3Propagator ApiMultiHeaderPropagator = new();
    private static readonly ApiB3Propagator ApiSingleHeaderPropagator = new(true);
    private static readonly ExtensionsB3Propagator ExtensionsMultiHeaderPropagator = new();
    private static readonly ExtensionsB3Propagator ExtensionsSingleHeaderPropagator = new(true);

    private static readonly Func<Dictionary<string, string>, string, IEnumerable<string>> Getter =
        static (carrier, name) => carrier.TryGetValue(name, out var value) ? [value] : [];

    private static readonly Action<Dictionary<string, string>, string, string> Setter =
        static (carrier, name, value) => carrier[name] = value;

    [Params(false, true)]
    public bool SingleHeader { get; set; }

    [Params(false, true)]
    public bool Sampled { get; set; }

    public Dictionary<string, string> ExtractCarrier { get; private set; } = [];

    public Dictionary<string, string> ApiInjectCarrier { get; private set; } = [];

    public Dictionary<string, string> ExtensionsInjectCarrier { get; private set; } = [];

    public PropagationContext InjectContext { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        this.ExtractCarrier = this.CreateExtractCarrier();
        this.InjectContext = new PropagationContext(
            new ActivityContext(
                TraceId,
                SpanId,
                this.Sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None),
            default);

        this.ApiInjectCarrier = [];
        this.ExtensionsInjectCarrier = [];
    }

    [Benchmark(Baseline = true)]
    public PropagationContext ApiExtract() =>
        this.GetApiPropagator().Extract(default, this.ExtractCarrier, Getter);

    [Benchmark]
    public PropagationContext ExtensionsExtract() =>
        this.GetExtensionsPropagator().Extract(default, this.ExtractCarrier, Getter);

    [Benchmark]
    public void ApiInject()
    {
        this.ApiInjectCarrier.Clear();
        this.GetApiPropagator().Inject(this.InjectContext, this.ApiInjectCarrier, Setter);
    }

    [Benchmark]
    public void ExtensionsInject()
    {
        this.ExtensionsInjectCarrier.Clear();
        this.GetExtensionsPropagator().Inject(this.InjectContext, this.ExtensionsInjectCarrier, Setter);
    }

    private static Dictionary<string, string> CreateMultiHeaderCarrier(bool sampled)
    {
        var carrier = new Dictionary<string, string>
        {
            [B3TraceId] = TraceIdBase16,
            [B3SpanId] = SpanIdBase16,
        };

        if (sampled)
        {
            carrier[B3Sampled] = SampledValue;
        }

        return carrier;
    }

    private static Dictionary<string, string> CreateSingleHeaderCarrier(bool sampled) =>
        new()
        {
            [B3Combined] = sampled
                ? $"{TraceIdBase16}-{SpanIdBase16}-{SampledValue}"
                : $"{TraceIdBase16}-{SpanIdBase16}",
        };

    private Dictionary<string, string> CreateExtractCarrier() =>
        this.SingleHeader
            ? CreateSingleHeaderCarrier(this.Sampled)
            : CreateMultiHeaderCarrier(this.Sampled);

    private ApiB3Propagator GetApiPropagator() =>
        this.SingleHeader ? ApiSingleHeaderPropagator : ApiMultiHeaderPropagator;

    private ExtensionsB3Propagator GetExtensionsPropagator() =>
        this.SingleHeader ? ExtensionsSingleHeaderPropagator : ExtensionsMultiHeaderPropagator;
}
