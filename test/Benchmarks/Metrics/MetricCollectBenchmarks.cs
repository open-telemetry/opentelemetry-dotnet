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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.400
  [Host]     : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  DefaultJob : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT


|  Method | UseWithRef |     Mean |    Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------- |----------- |---------:|---------:|---------:|------:|------:|------:|----------:|
| Collect |      False | 51.38 us | 1.027 us | 1.261 us |     - |     - |     - |     136 B |
| Collect |       True | 33.86 us | 0.716 us | 2.088 us |     - |     - |     - |     136 B |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class MetricCollectBenchmarks
    {
        private Counter<double> counter;
        private MeterProvider provider;
        private Meter meter;
        private CancellationTokenSource token;
        private BaseExportingMetricReader reader;
        private Task writeMetricTask;
        private string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

        // TODO: Confirm if this needs to be thread-safe
        private Random random = new Random();

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
                        foreach (ref var metricPoint in metric.GetMetricPoints())
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
                Temporality = AggregationTemporality.Cumulative,
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
}
