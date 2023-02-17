// <copyright file="ExemplarBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
// * Summary *
BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22621.1265)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.103
  [Host]     : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2


|          Method | EnableExemplar |     Mean |   Error |  StdDev | Allocated |
|---------------- |--------------- |---------:|--------:|--------:|----------:|
| HistogramUpdate |          False | 249.6 ns | 4.42 ns | 7.13 ns |         - |
| HistogramUpdate |           True | 284.2 ns | 5.39 ns | 5.04 ns |         - |

*/

namespace Benchmarks.Metrics
{
    public class ExemplarBenchmarks
    {
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
        private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
        private Histogram<long> histogram;
        private MeterProvider provider;
        private Meter meter;

        [Params(true, false)]
        public bool EnableExemplar { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.histogram = this.meter.CreateHistogram<long>("histogram");
            var exportedItems = new List<Metric>();

            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .SetExemplarFilter(this.EnableExemplar ? new AlwaysOnExemplarFilter() : new AlwaysOffExemplarFilter())
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                })
                .Build();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void HistogramUpdate()
        {
            var random = ThreadLocalRandom.Value;
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[random.Next(0, 10)] },
            };

            this.histogram.Record(random.Next(1000), tags);
        }
    }
}
