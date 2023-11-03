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
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100-rc.2.23502.2
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


| Method                    | ExemplarFilter | Mean     | Error   | StdDev  |
|-------------------------- |--------------- |---------:|--------:|--------:|
| HistogramNoTagReduction   | AlwaysOff      | 316.7 ns | 1.01 ns | 0.89 ns |
| HistogramWithTagReduction | AlwaysOff      | 298.9 ns | 2.44 ns | 2.16 ns |
| HistogramNoTagReduction   | AlwaysOn       | 359.6 ns | 1.26 ns | 1.11 ns |
| HistogramWithTagReduction | AlwaysOn       | 400.1 ns | 4.16 ns | 3.90 ns |
| HistogramNoTagReduction   | HighValueOnly  | 325.3 ns | 3.33 ns | 2.78 ns |
| HistogramWithTagReduction | HighValueOnly  | 320.7 ns | 4.91 ns | 4.60 ns |
*/

namespace Benchmarks.Metrics;

public class ExemplarBenchmarks
{
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
    private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
    private Histogram<long> histogramWithoutTagReduction;

    private Histogram<long> histogramWithTagReduction;

    private MeterProvider meterProvider;
    private Meter meter;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Test only.")]
    public enum ExemplarFilterToUse
    {
        AlwaysOff,
        AlwaysOn,
        HighValueOnly,
    }

    [Params(ExemplarFilterToUse.AlwaysOn, ExemplarFilterToUse.AlwaysOff, ExemplarFilterToUse.HighValueOnly)]
    public ExemplarFilterToUse ExemplarFilter { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.histogramWithoutTagReduction = this.meter.CreateHistogram<long>("HistogramWithoutTagReduction");
        this.histogramWithTagReduction = this.meter.CreateHistogram<long>("HistogramWithTagReduction");
        var exportedItems = new List<Metric>();

        ExemplarFilter exemplarFilter = new AlwaysOffExemplarFilter();
        if (this.ExemplarFilter == ExemplarFilterToUse.AlwaysOn)
        {
            exemplarFilter = new AlwaysOnExemplarFilter();
        }
        else if (this.ExemplarFilter == ExemplarFilterToUse.HighValueOnly)
        {
            exemplarFilter = new HighValueExemplarFilter();
        }

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .SetExemplarFilter(exemplarFilter)
            .AddView("HistogramWithTagReduction", new MetricStreamConfiguration() { TagKeys = new string[] { "DimName1", "DimName2", "DimName3" } })
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
        this.meterProvider.Dispose();
    }

    [Benchmark]
    public void HistogramNoTagReduction()
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

        this.histogramWithoutTagReduction.Record(random.Next(1000), tags);
    }

    [Benchmark]
    public void HistogramWithTagReduction()
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

        this.histogramWithTagReduction.Record(random.Next(1000), tags);
    }

    internal class HighValueExemplarFilter : ExemplarFilter
    {
        public override bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            return value > 800;
        }

        public override bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            return value > 800;
        }
    }
}
