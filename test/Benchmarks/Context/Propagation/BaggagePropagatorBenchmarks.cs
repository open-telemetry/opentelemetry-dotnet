// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Context.Propagation;

[MemoryDiagnoser]
public class BaggagePropagatorBenchmarks
{
    private static readonly BaggagePropagator Propagator = new();

    private static readonly Func<Dictionary<string, string>, string, IEnumerable<string>> Getter =
        static (carrier, name) => carrier.TryGetValue(name, out var value) ? [value] : [];

    private static readonly Action<Dictionary<string, string>, string, string> Setter =
        static (carrier, name, value) => carrier[name] = value;

    /// <summary>Gets or sets the number of baggage entries used in each benchmark run.</summary>
    [Params(1, 5, 20)]
    public int ItemCount { get; set; }

    /// <summary>Gets or sets a value indicating whether keys and values contain characters that require URL-encoding.</summary>
    [Params(false, true)]
    public bool UseSpecialChars { get; set; }

    /// <summary>
    /// Gets or sets the header style used in each benchmark run.
    /// </summary>
    [Params("W3C", "Properties")]
    public string HeaderStyle { get; set; } = "Clean";

    public Dictionary<string, string> ExtractCarrier { get; private set; } = [];

    public Dictionary<string, string> InjectCarrier { get; private set; } = [];

    public PropagationContext InjectContext { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        IEnumerable<(string Key, string Value)> Items() =>
            Enumerable.Range(0, this.ItemCount).Select(i =>
                this.UseSpecialChars
                    ? ($"key {i}", $"value {i} !@#$%^&*()")
                    : ($"key{i}", $"value{i}"));

        var baggageHeader = this.HeaderStyle switch
        {
            "W3C" => string.Join(" , ", Items().Select(p =>
            $"{Uri.EscapeDataString(p.Key)} = {Uri.EscapeDataString(p.Value)} ; prop1 ; propKey=propValue")),
            _ => string.Join(",", Items().Select(p =>
                    $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}")),
        };

        this.ExtractCarrier = new Dictionary<string, string>
        {
            ["baggage"] = baggageHeader,
        };

        var baggageDict = Items().ToDictionary(p => p.Key, p => p.Value);
        this.InjectContext = new PropagationContext(default, Baggage.Create(baggageDict));
        this.InjectCarrier = [];
    }

    [Benchmark]
    public PropagationContext Extract() =>
        Propagator.Extract(default, this.ExtractCarrier, Getter);

    [Benchmark]
    public void Inject()
    {
        this.InjectCarrier.Clear();
        Propagator.Inject(this.InjectContext, this.InjectCarrier, Setter);
    }
}
