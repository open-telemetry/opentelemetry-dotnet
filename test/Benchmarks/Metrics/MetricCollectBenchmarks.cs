// <copyright file="MetricCollectBenchmarks.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|  Method | UseWithRef |     Mean |    Error |   StdDev | Allocated |
|-------- |----------- |---------:|---------:|---------:|----------:|
| Collect |      False | 18.45 us | 0.161 us | 0.151 us |      96 B |
| Collect |       True | 17.71 us | 0.347 us | 0.644 us |      96 B |
*/

namespace Benchmarks.Metrics;

public class MetricCollectBenchmarks
{
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

    // TODO: Confirm if this needs to be thread-safe
    private readonly Random random = new();
    private Counter<double> counter;
    private MeterProvider provider;
    private Meter meter;
    private CancellationTokenSource token;
    private BaseExportingMetricReader reader;
    private Task writeMetricTask;

    [Params(false, true)]
    public bool UseWithRef { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var metricExporter = new TestExporter<Metric>(ProcessExport);
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
                var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
                var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
                var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
                this.counter.Add(100.00, tag1, tag2, tag3);
            }
        });
        this.writeMetricTask.Start();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.token.Cancel();
        this.token.Dispose();
        this.writeMetricTask.Wait();
        this.meter.Dispose();
        this.provider.Dispose();
    }

    [Benchmark]
    public void Collect()
    {
        this.reader.Collect();
    }
}
