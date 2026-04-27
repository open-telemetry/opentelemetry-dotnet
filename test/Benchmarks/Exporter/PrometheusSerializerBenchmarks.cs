// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class PrometheusSerializerBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private readonly List<Metric> metrics = [];
    private readonly byte[] buffer = new byte[85000];
    private readonly Dictionary<Metric, PrometheusMetric> cache = [];
    private Metric? histogramMetric;
    private Metric? typedLabelsMetric;
    private Meter? meter;
    private MeterProvider? meterProvider;

    [Params(1, 1000, 10000)]
    public int NumberOfSerializeCalls { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(this.metrics)
            .Build();

        var counter = this.meter.CreateCounter<long>("counter_name_1", "long", "counter_name_1_description");
        counter.Add(18, new("label1", "value1"), new("label2", "value2"));

        var gauge = this.meter.CreateObservableGauge("gauge_name_1", () => 18.0D, "long", "gauge_name_1_description");

        var histogram = this.meter.CreateHistogram<long>("histogram_name_1", "long", "histogram_name_1_description");
        histogram.Record(100, new("label1", "value1"), new("label2", "value2"));

        var typedLabelsCounter = this.meter.CreateCounter<long>("counter_name_2", "long", "counter_name_2_description");
        typedLabelsCounter.Add(18, new("bool_label", true), new("long_label", 9223372036854775807L), new("double_label", 1234.5));

        this.meterProvider.ForceFlush();

        this.histogramMetric = this.metrics.Single(metric => metric.Name == "histogram_name_1");
        this.typedLabelsMetric = this.metrics.Single(metric => metric.Name == "counter_name_2");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.meter?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void WriteMetric()
    {
        for (var i = 0; i < this.NumberOfSerializeCalls; i++)
        {
            var cursor = 0;
            foreach (var metric in this.metrics)
            {
                cursor = PrometheusSerializer.WriteMetric(this.buffer, cursor, metric, this.GetPrometheusMetric(metric), openMetricsRequested: false);
            }
        }
    }

    [Benchmark]
    public void WriteHistogramMetric()
    {
        for (var i = 0; i < this.NumberOfSerializeCalls; i++)
        {
            _ = PrometheusSerializer.WriteMetric(this.buffer, 0, this.histogramMetric!, this.GetPrometheusMetric(this.histogramMetric!), openMetricsRequested: false);
        }
    }

    [Benchmark]
    public void WriteMetricWithTypedLabels()
    {
        for (var i = 0; i < this.NumberOfSerializeCalls; i++)
        {
            _ = PrometheusSerializer.WriteMetric(this.buffer, 0, this.typedLabelsMetric!, this.GetPrometheusMetric(this.typedLabelsMetric!), openMetricsRequested: false);
        }
    }

    private PrometheusMetric GetPrometheusMetric(Metric metric)
    {
        if (!this.cache.TryGetValue(metric, out var prometheusMetric))
        {
            prometheusMetric = PrometheusMetric.Create(metric, false);
            this.cache[metric] = prometheusMetric;
        }

        return prometheusMetric;
    }
}
