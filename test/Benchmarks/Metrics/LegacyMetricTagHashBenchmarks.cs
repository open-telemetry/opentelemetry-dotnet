// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

/// <summary>
/// Measures the compatibility cost of hashing Guid and decimal metric tags.
/// </summary>
[MemoryDiagnoser]
#pragma warning disable CA1001 // Disposable fields are disposed in GlobalCleanup.
public class LegacyMetricTagHashBenchmarks
#pragma warning restore CA1001
{
    private const int CardinalityLimit = 2000;

    private Meter? meter;
    private MeterProvider? provider;
    private Counter<long>? counter;
    private object? existingValue;
    private object? unseenValue;

    [Params("Guid", "Decimal")]
    public string ValueType { get; set; } = "Guid";

    [Params(false, true)]
    public bool Colliding { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(new MeterOptions(nameof(LegacyMetricTagHashBenchmarks)));
        this.provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(new List<Metric>())
            .Build();
        this.counter = this.meter.CreateCounter<long>("counter");

        for (var i = 1; i <= CardinalityLimit; i++)
        {
            this.counter.Add(1, new KeyValuePair<string, object?>("customer", this.CreateValue(i)));
        }

        this.existingValue = this.CreateValue(CardinalityLimit / 2);
        this.unseenValue = this.CreateValue(CardinalityLimit + 1);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.provider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void ExistingTagSet()
        => this.counter!.Add(1, new KeyValuePair<string, object?>("customer", this.existingValue));

    [Benchmark]
    public void RecordAtLimit()
        => this.counter!.Add(1, new KeyValuePair<string, object?>("customer", this.unseenValue));

    private object CreateValue(int i)
    {
        if (!this.Colliding)
        {
            return this.ValueType == "Guid" ? (object)new Guid(i, 0, 0, new byte[8]) : (decimal)i;
        }

#if NETFRAMEWORK
        // Framework hashes decimals through double and combines a subset of Guid fields.
        return this.ValueType == "Guid"
            ? (object)new Guid(i, 0, (short)i, 0, 0, 0, 0, 0, 0, 0, 0)
            : new decimal(i, 0, 1, false, 0);
#else
        return this.ValueType == "Guid"
            ? (object)new Guid(i, 0, 0, (byte)i, (byte)(i >> 8), 0, 0, 0, 0, 0, 0)
            : new decimal(i, i, 0, false, 0);
#endif
    }
}
