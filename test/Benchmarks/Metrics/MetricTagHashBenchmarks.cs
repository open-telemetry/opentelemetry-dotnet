// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

/// <summary>
/// Measures overflow lookups after filling metric streams with ordinary or
/// colliding tag values. Tag values are boxed during setup in every case.
/// </summary>
[MemoryDiagnoser]
#pragma warning disable CA1001 // Disposable fields are disposed in GlobalCleanup.
public class MetricTagHashBenchmarks
#pragma warning restore CA1001
{
    private const int CardinalityLimit = 2000;

    private readonly object ordinaryLong = ((long)(CardinalityLimit + 1) << 32) | 7;
    private readonly object collidingLong = ((long)(CardinalityLimit + 1) << 32) | (CardinalityLimit + 1);
    private readonly object integer = CardinalityLimit + 1;
    private Meter? meter;
    private MeterProvider? provider;
    private Counter<long>? ordinaryLongCounter;
    private Counter<long>? collidingLongCounter;
    private Counter<long>? stringCounter;
    private Counter<long>? integerCounter;

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(new MeterOptions(nameof(MetricTagHashBenchmarks)));
        this.provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(new List<Metric>())
            .Build();

        this.ordinaryLongCounter = this.meter.CreateCounter<long>("ordinary-long");
        this.collidingLongCounter = this.meter.CreateCounter<long>("colliding-long");
        this.stringCounter = this.meter.CreateCounter<long>("string");
        this.integerCounter = this.meter.CreateCounter<long>("integer");

        for (var i = 1; i <= CardinalityLimit; i++)
        {
            this.ordinaryLongCounter.Add(1, new KeyValuePair<string, object?>("customer", ((long)i << 32) | 7));
            this.collidingLongCounter.Add(1, new KeyValuePair<string, object?>("customer", ((long)i << 32) | (uint)i));
            this.stringCounter.Add(1, new KeyValuePair<string, object?>("customer", i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            this.integerCounter.Add(1, new KeyValuePair<string, object?>("customer", i));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.provider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void OrdinaryLongAtLimit()
        => this.ordinaryLongCounter!.Add(1, new KeyValuePair<string, object?>("customer", this.ordinaryLong));

    [Benchmark]
    public void CollidingLongAtLimit()
        => this.collidingLongCounter!.Add(1, new KeyValuePair<string, object?>("customer", this.collidingLong));

    [Benchmark]
    public void StringAtLimit()
        => this.stringCounter!.Add(1, new KeyValuePair<string, object?>("customer", "unseen"));

    [Benchmark]
    public void Int32AtLimit()
        => this.integerCounter!.Add(1, new KeyValuePair<string, object?>("customer", this.integer));
}
