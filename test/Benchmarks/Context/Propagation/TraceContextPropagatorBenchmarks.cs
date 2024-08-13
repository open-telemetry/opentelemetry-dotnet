// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Context.Propagation;

public class TraceContextPropagatorBenchmarks
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string SpanId = "b9c7c989f97918e1";

    private static readonly Random Random = new(455946);
    private static readonly TraceContextPropagator TraceContextPropagator = new();

    private static readonly Func<IReadOnlyDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
    {
        if (headers.TryGetValue(name, out var value))
        {
            return [value];
        }

        return [];
    };

    private Dictionary<string, string> headers;

    [Params(true, false)]
    public bool LongListMember { get; set; }

    [Params(0, 4, 32)]
    public int MembersCount { get; set; }

    public Dictionary<string, string> Headers => this.headers;

    [GlobalSetup]
    public void Setup()
    {
        var length = this.LongListMember ? 256 : 20;

        var value = new string('a', length);

        Span<char> keyBuffer = stackalloc char[length - 2];

        string traceState = string.Empty;
        for (var i = 0; i < this.MembersCount; i++)
        {
            // We want a unique key for each member
            for (var j = 0; j < length - 2; j++)
            {
                keyBuffer[j] = (char)('a' + Random.Next(0, 26));
            }

            var key = keyBuffer.ToString();

            var listMember = $"{key}{i:00}={value}";
            traceState += i < this.MembersCount - 1 ? $"{listMember}," : listMember;
        }

        this.headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-01" },
            { TraceState, traceState },
        };
    }

    [Benchmark(Baseline = true)]
    public void Extract() => _ = TraceContextPropagator!.Extract(default, this.headers, Getter);
}
