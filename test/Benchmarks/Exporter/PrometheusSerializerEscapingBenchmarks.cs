// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Exporter.Prometheus.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Exporter;

/// <summary>
/// Compares the serialization throughput and allocation of the Prometheus serializer across every
/// combination of the text formats and escaping schemes.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class PrometheusSerializerEscapingBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private readonly List<Metric> metrics = [];
    private readonly byte[] buffer = new byte[85000];
    private readonly Dictionary<Metric, PrometheusMetric> cache = [];
    private Meter? meter;
    private MeterProvider? meterProvider;
    private TextFormatSerializer? serializer;

    /// <summary>
    /// The Prometheus exposition text format and version to serialize with.
    /// </summary>
    public enum TextFormat
    {
        /// <summary>The Prometheus text format version 0.0.4.</summary>
        PrometheusTextV0,

        /// <summary>The Prometheus text format version 1.0.0.</summary>
        PrometheusTextV1,

        /// <summary>The OpenMetrics text format version 0.0.1.</summary>
        OpenMetricsV0,

        /// <summary>The OpenMetrics text format version 1.0.0.</summary>
        OpenMetricsV1,
    }

    [Params(TextFormat.PrometheusTextV0, TextFormat.PrometheusTextV1, TextFormat.OpenMetricsV0, TextFormat.OpenMetricsV1)]
    public TextFormat Format { get; set; }

    [Params(
        PrometheusProtocol.UnderscoresEscaping,
        PrometheusProtocol.DotsEscaping,
        PrometheusProtocol.ValuesEscaping,
        PrometheusProtocol.AllowUtf8Escaping)]
    public string Escaping { get; set; } = PrometheusProtocol.UnderscoresEscaping;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .AddInMemoryExporter(this.metrics)
            .Build();

        var counter = this.meter.CreateCounter<long>("http.server.request.count", "By", "Number of requests.");
        counter.Add(18, new("http.method", "GET"), new("http.route", "/api/values"));

        _ = this.meter.CreateObservableGauge("system.cpu.utilization", () => 0.42D, description: "CPU utilization.");

        var histogram = this.meter.CreateHistogram<double>("http.server.request.duration", "s", "Request duration.");
        histogram.Record(0.25, new("http.method", "GET"), new("http.route", "/api/values"));

        var typedLabels = this.meter.CreateCounter<long>("rpc.client.duration", "ms", "RPC duration.");
        typedLabels.Add(18, new("rpc.success", true), new("rpc.attempt", 3L), new("rpc.cost", 12.5));

        this.meterProvider.ForceFlush();

        var (mediaType, version, isOpenMetrics) = this.Format switch
        {
            TextFormat.PrometheusTextV0 => (PrometheusProtocol.PrometheusTextMediaType, PrometheusProtocol.PrometheusV0, false),
            TextFormat.PrometheusTextV1 => (PrometheusProtocol.PrometheusTextMediaType, PrometheusProtocol.PrometheusV1, false),
            TextFormat.OpenMetricsV0 => (PrometheusProtocol.OpenMetricsMediaType, PrometheusProtocol.OpenMetricsV0, true),
            TextFormat.OpenMetricsV1 => (PrometheusProtocol.OpenMetricsMediaType, PrometheusProtocol.OpenMetricsV1, true),
            _ => throw new NotSupportedException(),
        };

        var protocol = new PrometheusProtocol(mediaType, this.Escaping, version, isOpenMetrics);

        this.serializer = TextFormatSerializer.GetSerializer(protocol);

        // Warm the per-scheme name cache so the benchmark measures steady-state serialization, which
        // is what a long-running scrape endpoint experiences (the escaped name sets are computed
        // lazily once per scheme and then reused).
        this.WriteMetrics();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.meter?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void WriteMetrics()
    {
        var cursor = 0;
        foreach (var metric in this.metrics)
        {
            cursor = this.serializer!.WriteMetric(
                this.buffer,
                cursor,
                metric,
                this.GetPrometheusMetric(metric),
                writeType: true,
                writeUnit: true,
                writeHelp: true,
                unitOverride: null,
                helpOverride: null);
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
