// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class OtlpMetricExporterBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private Counter<long>? counter;
    private BaseExportingMetricReader? reader;
    private Histogram<long>? histogram;
    private MeterProvider? provider;
    private Meter? meter;
    private IDisposable? server;
    private string? serverHost;
    private int serverPort;

    private TagList scalarTags;
    private TagList filteredArrayTags;

    [GlobalSetup]
    public void Setup()
    {
        this.server = TestHttpServer.RunServer(
            static ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            },
            out this.serverHost,
            out this.serverPort);

        this.meter = new Meter(Utils.GetCurrentMethodName());
        this.counter = this.meter.CreateCounter<long>("counter");
        this.histogram = this.meter.CreateHistogram<long>("histogram");

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        this.reader = new BaseExportingMetricReader(new OtlpMetricExporter(options))
#pragma warning restore CA2000 // Dispose objects before losing scope
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
        };

        this.provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(this.histogram.Name, new MetricStreamConfiguration() { TagKeys = ["keep1", "keep2"] })
            .AddReader(this.reader)
            .Build();

        this.scalarTags = new TagList
        {
            { "byteTag", (byte)1 },
            { "sbyteTag", (sbyte)2 },
            { "shortTag", (short)3 },
            { "ushortTag", (ushort)4 },
            { "intTag", 5 },
            { "uintTag", 6U },
            { "longTag", 7L },
            { "floatTag", 8.5F },
            { "doubleTag", 9.5D },
        };

#pragma warning disable CA1861 // Avoid constant arrays as arguments
        this.filteredArrayTags = new TagList
        {
            { "keep1", "value1" },
            { "keep2", "value2" },
            { "charArrayTag", new[] { 'a', 'b', 'c' } },
            { "boolArrayTag", new[] { true, false, true } },
            { "sbyteArrayTag", new sbyte[] { 1, 2, 3 } },
            { "byteArrayTag", new byte[] { 4, 5, 6 } },
            { "shortArrayTag", new short[] { 7, 8, 9 } },
            { "ushortArrayTag", new ushort[] { 10, 11, 12 } },
            { "intArrayTag", new[] { 13, 14, 15 } },
            { "uintArrayTag", new uint[] { 16, 17, 18 } },
            { "longArrayTag", new long[] { 19, 20, 21 } },
            { "floatArrayTag", new[] { 1.5F, 2.5F, 3.5F } },
            { "doubleArrayTag", new[] { 4.5D, 5.5D, 6.5D } },
        };
#pragma warning restore CA1861 // Avoid constant arrays as arguments
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meter?.Dispose();
        this.provider?.Dispose();
        this.server?.Dispose();
    }

    [Benchmark]
    public void ExportCounterWithScalarTags()
    {
        this.counter!.Add(100, this.scalarTags);
        this.reader!.Collect();
    }

    [Benchmark]
    public void ExportHistogramWithFilteredArrayTags()
    {
        this.histogram!.Record(100, this.filteredArrayTags);
        this.reader!.Collect();
    }
}
