// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Context.Propagation;

/// <summary>Benchmarks baggage injection values that exercise Unicode and escaping paths.</summary>
[MemoryDiagnoser]
public class BaggagePropagatorUnicodeInjectionBenchmarks
{
    private static readonly BaggagePropagator Propagator = new();

    private static readonly Action<Dictionary<string, string>, string, string> Setter =
        static (carrier, name, value) => carrier[name] = value;

    private readonly Dictionary<string, string> carrier = [];

    private PropagationContext context;

    /// <summary>Gets or sets the value shape encoded during injection.</summary>
    [Params(
        "PlainAscii",
        "EscapedAscii",
        "RepeatedBmpUnicode",
        "SurrogatePairUnicode",
        "Mixed",
        "IsolatedSurrogate")]
    public string ValueKind { get; set; } = "PlainAscii";

    /// <summary>Gets or sets the number of repeated value units used during injection.</summary>
    [Params(8, 256)]
    public int RepetitionCount { get; set; }

    /// <summary>Builds the baggage context before the measured injection.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var value = this.ValueKind switch
        {
            "EscapedAscii" => string.Concat(Enumerable.Repeat("a% ", this.RepetitionCount)),
            "RepeatedBmpUnicode" => string.Concat(Enumerable.Repeat("\u00E9", this.RepetitionCount)),
            "SurrogatePairUnicode" => string.Concat(Enumerable.Repeat("\U0001F600", this.RepetitionCount)),
            "Mixed" => string.Concat(Enumerable.Repeat("x\u00E9-\U0001F600-%", this.RepetitionCount)),
            "IsolatedSurrogate" => string.Concat(
                new string('a', this.RepetitionCount),
                "\uD800",
                new string('b', this.RepetitionCount)),
            _ => string.Concat(Enumerable.Repeat("abcXYZ09-_", this.RepetitionCount)),
        };

        this.context = new PropagationContext(
            default,
            Baggage.Create(new Dictionary<string, string> { ["key"] = value }));
    }

    /// <summary>Injects the prebuilt baggage context and returns its serialized header.</summary>
    /// <returns>The generated baggage header.</returns>
    [Benchmark]
    public string Inject()
    {
        this.carrier.Clear();
        Propagator.Inject(this.context, this.carrier, Setter);
        return this.carrier["baggage"];
    }
}
