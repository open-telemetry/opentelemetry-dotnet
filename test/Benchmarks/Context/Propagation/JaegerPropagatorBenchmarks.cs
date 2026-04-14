// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.Propagators;

namespace Benchmarks.Context.Propagation;

[MemoryDiagnoser]
[Obsolete("Intentional coverage for obsolete API.")]
public class JaegerPropagatorBenchmarks
{
    private const string TraceIdBase16 = "0007651916cd43dd8448eb211c803177";
    private const string SpanIdBase16 = "0007c989f9791877";
    private const string ParentSpanId = "0";
    private const string JaegerHeader = "uber-trace-id";
    private const string JaegerDelimiter = ":";
    private const string JaegerDelimiterEncoded = "%3A";
    private const string SampledValue = "1";

    private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromString(TraceIdBase16.AsSpan());
    private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromString(SpanIdBase16.AsSpan());
    private static readonly JaegerPropagator Propagator = new();

    private static readonly Func<Dictionary<string, string[]>, string, IEnumerable<string>> Getter =
        static (carrier, name) => carrier.TryGetValue(name, out var values) ? values : [];

    private static readonly Action<Dictionary<string, string>, string, string> Setter =
        static (carrier, name, value) => carrier[name] = value;

    [Params(false, true)]
    public bool Sampled { get; set; }

    [Params(false, true)]
    public bool UseEncodedDelimiter { get; set; }

    public Dictionary<string, string[]> ExtractCarrier { get; private set; } = [];

    public Dictionary<string, string> InjectCarrier { get; private set; } = [];

    public PropagationContext InjectContext { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        var delimiter = this.UseEncodedDelimiter
            ? JaegerDelimiterEncoded
            : JaegerDelimiter;
        var flags = this.Sampled ? SampledValue : "0";
        var headerValue = string.Join(delimiter, TraceIdBase16, SpanIdBase16, ParentSpanId, flags);

        this.ExtractCarrier = new Dictionary<string, string[]>
        {
            [JaegerHeader] = [headerValue],
        };

        this.InjectContext = new PropagationContext(
            new ActivityContext(
                TraceId,
                SpanId,
                this.Sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None),
            default);

        this.InjectCarrier = [];
    }

    [Benchmark(Baseline = true)]
    public PropagationContext Extract() =>
        Propagator.Extract(default, this.ExtractCarrier, Getter);

    [Benchmark]
    public void Inject()
    {
        this.InjectCarrier.Clear();
        Propagator.Inject(this.InjectContext, this.InjectCarrier, Setter);
    }
}
