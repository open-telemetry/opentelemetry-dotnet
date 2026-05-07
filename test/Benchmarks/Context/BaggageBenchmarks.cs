// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry;

namespace Benchmarks.Context;

[MemoryDiagnoser]
public class BaggageBenchmarks
{
    private KeyValuePair<string, string?>[] changedItems = [];
    private KeyValuePair<string, string?>[] sameItems = [];
    private Baggage baggage;
    private Baggage baggageAlias;
    private Baggage equalBaggage;

    [Params(1, 5, 20)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.sameItems = Enumerable.Range(0, this.ItemCount)
            .Select(i => new KeyValuePair<string, string?>($"key{i}", $"value{i}"))
            .ToArray();

        this.changedItems = this.sameItems
            .Select((item, index) => index == this.sameItems.Length - 1
                ? new KeyValuePair<string, string?>(item.Key, $"{item.Value}-updated")
                : item)
            .ToArray();

        var baggageItems = this.sameItems.ToDictionary(item => item.Key, item => item.Value!);

        this.baggage = Baggage.Create(baggageItems);
        this.baggageAlias = this.baggage;
        this.equalBaggage = Baggage.Create(baggageItems);
    }

    [Benchmark]
    public Baggage SetBaggageNoOp()
        => this.baggage.SetBaggage(this.sameItems);

    [Benchmark]
    public Baggage SetBaggageSingleUpdate()
        => this.baggage.SetBaggage(this.changedItems);

    [Benchmark]
    public Baggage RemoveMissingBaggage()
        => this.baggage.RemoveBaggage("missing-key");

    [Benchmark]
    public bool EqualsSameBackingStore()
        => this.baggage.Equals(this.baggageAlias);

    [Benchmark]
    public bool EqualsEquivalentContents()
        => this.baggage.Equals(this.equalBaggage);
}
