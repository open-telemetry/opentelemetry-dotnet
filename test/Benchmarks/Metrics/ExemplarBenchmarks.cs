// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3155/23H2/2023Update/SunValley3)
12th Gen Intel Core i9-12900HK, 1 CPU, 20 logical and 14 physical cores
.NET SDK 8.0.200
  [Host]     : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2


| Method                    | ExemplarConfiguration | Mean     | Error   | StdDev  | Allocated |
|-------------------------- |---------------------- |---------:|--------:|--------:|----------:|
| HistogramNoTagReduction   | AlwaysOff             | 174.6 ns | 1.32 ns | 1.24 ns |         - |
| HistogramWithTagReduction | AlwaysOff             | 161.8 ns | 2.63 ns | 2.46 ns |         - |
| CounterNoTagReduction     | AlwaysOff             | 141.6 ns | 2.12 ns | 1.77 ns |         - |
| CounterWithTagReduction   | AlwaysOff             | 141.7 ns | 2.11 ns | 1.87 ns |         - |
| HistogramNoTagReduction   | AlwaysOn              | 201.1 ns | 3.05 ns | 2.86 ns |         - |
| HistogramWithTagReduction | AlwaysOn              | 196.5 ns | 1.91 ns | 1.78 ns |         - |
| CounterNoTagReduction     | AlwaysOn              | 149.7 ns | 1.42 ns | 1.33 ns |         - |
| CounterWithTagReduction   | AlwaysOn              | 143.5 ns | 2.09 ns | 1.95 ns |         - |
| HistogramNoTagReduction   | TraceBased            | 171.9 ns | 2.33 ns | 2.18 ns |         - |
| HistogramWithTagReduction | TraceBased            | 164.9 ns | 2.70 ns | 2.52 ns |         - |
| CounterNoTagReduction     | TraceBased            | 148.1 ns | 2.76 ns | 2.58 ns |         - |
| CounterWithTagReduction   | TraceBased            | 141.2 ns | 1.43 ns | 1.34 ns |         - |
| HistogramNoTagReduction   | Alway(...)pling [29]  | 183.9 ns | 1.49 ns | 1.39 ns |         - |
| HistogramWithTagReduction | Alway(...)pling [29]  | 176.1 ns | 3.35 ns | 3.29 ns |         - |
| CounterNoTagReduction     | Alway(...)pling [29]  | 159.3 ns | 3.12 ns | 4.38 ns |         - |
| CounterWithTagReduction   | Alway(...)pling [29]  | 158.7 ns | 3.06 ns | 3.65 ns |         - |
*/

namespace Benchmarks.Metrics;

public class ExemplarBenchmarks
{
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
    private readonly string[] dimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];
    private Histogram<double>? histogramWithoutTagReduction;
    private Histogram<double>? histogramWithTagReduction;
    private Counter<long>? counterWithoutTagReduction;
    private Counter<long>? counterWithTagReduction;
    private MeterProvider? meterProvider;
    private Meter? meter;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Test only.")]
    public enum ExemplarConfigurationType
    {
        AlwaysOff,
        AlwaysOn,
        TraceBased,
        AlwaysOnWithHighValueSampling,
    }

    [Params(ExemplarConfigurationType.AlwaysOn, ExemplarConfigurationType.AlwaysOff, ExemplarConfigurationType.TraceBased, ExemplarConfigurationType.AlwaysOnWithHighValueSampling)]
    public ExemplarConfigurationType ExemplarConfiguration { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogramWithoutTagReduction = this.meter.CreateHistogram<double>("HistogramWithoutTagReduction");
        this.histogramWithTagReduction = this.meter.CreateHistogram<double>("HistogramWithTagReduction");
        this.counterWithoutTagReduction = this.meter.CreateCounter<long>("CounterWithoutTagReduction");
        this.counterWithTagReduction = this.meter.CreateCounter<long>("CounterWithTagReduction");
        var exportedItems = new List<Metric>();

        var exemplarFilter = this.ExemplarConfiguration == ExemplarConfigurationType.TraceBased
            ? ExemplarFilterType.TraceBased
            : this.ExemplarConfiguration != ExemplarConfigurationType.AlwaysOff
                ? ExemplarFilterType.AlwaysOn
                : ExemplarFilterType.AlwaysOff;

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .SetExemplarFilter(exemplarFilter)
            .AddView(i =>
            {
#if NET
                if (i.Name.Contains("WithTagReduction", StringComparison.Ordinal))
#else
                if (i.Name.Contains("WithTagReduction"))
#endif
                {
                    return new MetricStreamConfiguration()
                    {
                        TagKeys = ["DimName1", "DimName2", "DimName3"],
                        ExemplarReservoirFactory = CreateExemplarReservoir,
                    };
                }
                else
                {
                    return new MetricStreamConfiguration()
                    {
                        ExemplarReservoirFactory = CreateExemplarReservoir,
                    };
                }
            })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            })
            .Build();

        ExemplarReservoir? CreateExemplarReservoir()
        {
            return this.ExemplarConfiguration == ExemplarConfigurationType.AlwaysOnWithHighValueSampling
                ? new HighValueExemplarReservoir(800D)
                : null;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void HistogramNoTagReduction()
    {
        var random = ThreadLocalRandom.Value!;
        var tags = new TagList
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            { "DimName1", this.dimensionValues[random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[random.Next(0, 10)] },
        };

        this.histogramWithoutTagReduction!.Record(random.NextDouble() * 1000D, tags);
    }

    [Benchmark]
    public void HistogramWithTagReduction()
    {
        var random = ThreadLocalRandom.Value!;
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[random.Next(0, 10)] },
        };

        this.histogramWithTagReduction!.Record(random.NextDouble() * 1000D, tags);
    }

    [Benchmark]
    public void CounterNoTagReduction()
    {
        var random = ThreadLocalRandom.Value!;
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[random.Next(0, 10)] },
        };

        this.counterWithoutTagReduction!.Add(random.Next(1000), tags);
    }

    [Benchmark]
    public void CounterWithTagReduction()
    {
        var random = ThreadLocalRandom.Value!;
        var tags = new TagList
        {
            { "DimName1", this.dimensionValues[random.Next(0, 2)] },
            { "DimName2", this.dimensionValues[random.Next(0, 2)] },
            { "DimName3", this.dimensionValues[random.Next(0, 5)] },
            { "DimName4", this.dimensionValues[random.Next(0, 5)] },
            { "DimName5", this.dimensionValues[random.Next(0, 10)] },
        };

        this.counterWithTagReduction!.Add(random.Next(1000), tags);
#pragma warning restore CA5394 // Do not use insecure randomness
    }

    private sealed class HighValueExemplarReservoir : FixedSizeExemplarReservoir
    {
        private readonly double threshold;
        private int measurementCount;

        public HighValueExemplarReservoir(double threshold)
            : base(10)
        {
            this.threshold = threshold;
        }

        public override void Offer(in ExemplarMeasurement<long> measurement)
        {
            if (measurement.Value >= this.threshold)
            {
                this.UpdateExemplar(this.measurementCount++ % this.Capacity, in measurement);
            }
        }

        public override void Offer(in ExemplarMeasurement<double> measurement)
        {
            if (measurement.Value >= this.threshold)
            {
                this.UpdateExemplar(this.measurementCount++ % this.Capacity, in measurement);
            }
        }

        protected override void OnCollected()
        {
            this.measurementCount = 0;
        }
    }
}
