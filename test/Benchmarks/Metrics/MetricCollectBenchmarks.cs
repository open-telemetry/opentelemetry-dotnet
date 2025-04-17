// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method  | UseWithRef | Mean     | Error    | StdDev   | Allocated |
|-------- |----------- |---------:|---------:|---------:|----------:|
| Collect | False      | 21.03 us | 0.148 us | 0.361 us |      96 B |
| Collect | True       | 20.37 us | 0.399 us | 0.559 us |      96 B |
*/

namespace Benchmarks.Metrics;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class MetricCollectBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private readonly string[] dimensionValues = ["DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10"];

    // TODO: Confirm if this needs to be thread-safe
    private readonly Random random = new();
    private Counter<double>? counter;
    private MeterProvider? provider;
    private Meter? meter;
    private CancellationTokenSource? token;
    private BaseExportingMetricReader? reader;
    private Task? writeMetricTask;

    [Params(false, true)]
    public bool UseWithRef { get; set; }

    [GlobalSetup]
    public void Setup()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var metricExporter = new TestExporter<Metric>(ProcessExport);
#pragma warning restore CA2000 // Dispose objects before losing scope
        void ProcessExport(Batch<Metric> batch)
        {
            double sum = 0;
            foreach (var metric in batch)
            {
                if (this.UseWithRef)
                {
                    // The performant way of iterating.
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        sum += metricPoint.GetSumDouble();
                    }
                }
                else
                {
                    // The non-performant way of iterating.
                    // This is still "correct", but less performant.
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        sum += metricPoint.GetSumDouble();
                    }
                }
            }
        }

        this.reader = new BaseExportingMetricReader(metricExporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
        };

        this.meter = new Meter(Utils.GetCurrentMethodName());

        this.provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddReader(this.reader)
            .Build();

        this.counter = this.meter.CreateCounter<double>("counter");
        this.token = new CancellationTokenSource();
        this.writeMetricTask = new Task(() =>
        {
            while (!this.token.IsCancellationRequested)
            {
#pragma warning disable CA5394 // Do not use insecure randomness
                var tag1 = new KeyValuePair<string, object?>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
                var tag2 = new KeyValuePair<string, object?>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
                var tag3 = new KeyValuePair<string, object?>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
#pragma warning restore CA5394 // Do not use insecure randomness
                this.counter.Add(100.00, tag1, tag2, tag3);
            }
        });
        this.writeMetricTask.Start();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.token?.Cancel();
        this.token?.Dispose();
        this.writeMetricTask?.Wait();
        this.meter?.Dispose();
        this.provider?.Dispose();
    }

    [Benchmark]
    public void Collect()
    {
        this.reader!.Collect();
    }
}
