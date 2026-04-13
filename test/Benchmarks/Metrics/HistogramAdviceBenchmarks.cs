// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

[MemoryDiagnoser]
public class HistogramAdviceBenchmarks
{
    [Params(
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double))]
    public Type ValueType { get; set; } = default!;

    [Benchmark]
    public int BuildProviderAndCollect()
    {
        using var meter = new Meter($"{nameof(HistogramAdviceBenchmarks)}-{this.ValueType}");
        var exportedItems = new List<Metric>();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        switch (this.ValueType)
        {
            case Type t when t == typeof(byte):
                var byteHistogram = meter.CreateHistogram<byte>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10, 20] });
                byteHistogram.Record(9);
                byteHistogram.Record(19);
                break;

            case Type t when t == typeof(short):
                var shortHistogram = meter.CreateHistogram<short>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10, 20] });
                shortHistogram.Record(9);
                shortHistogram.Record(19);
                break;

            case Type t when t == typeof(int):
                var intHistogram = meter.CreateHistogram<int>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10, 20] });
                intHistogram.Record(9);
                intHistogram.Record(19);
                break;

            case Type t when t == typeof(long):
                var longHistogram = meter.CreateHistogram<long>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10, 20] });
                longHistogram.Record(9);
                longHistogram.Record(19);
                break;

            case Type t when t == typeof(float):
                var floatHistogram = meter.CreateHistogram<float>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10.0f, 20.0f] });
                floatHistogram.Record(9.0f);
                floatHistogram.Record(19.0f);
                break;

            case Type t when t == typeof(double):
                var doubleHistogram = meter.CreateHistogram<double>(
                    "histogram",
                    unit: null,
                    description: null,
                    tags: null,
                    new() { HistogramBucketBoundaries = [10.0, 20.0] });
                doubleHistogram.Record(9.0);
                doubleHistogram.Record(19.0);
                break;

            default:
                throw new NotSupportedException($"Unsupported histogram value type: {this.ValueType}");
        }

        provider.ForceFlush();

        var totalCount = 0;
        foreach (ref readonly var metricPoint in exportedItems[0].GetMetricPoints())
        {
            totalCount += (int)metricPoint.GetHistogramCount();
        }

        return totalCount;
    }
}
