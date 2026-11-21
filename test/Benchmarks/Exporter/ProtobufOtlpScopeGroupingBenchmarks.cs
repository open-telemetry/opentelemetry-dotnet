// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Measures how the OTLP trace and metric serializers group a batch by
/// instrumentation scope. The items carry no attributes, so the cost of grouping
/// is a large share of each run.
/// </summary>
[MemoryDiagnoser(false)]
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class ProtobufOtlpScopeGroupingBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const int ItemCount = 256;

    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly List<ActivitySource> activitySources = [];
    private readonly List<Meter> meters = [];
    private byte[] buffer = null!;
    private ActivityListener? activityListener;
    private MeterProvider? meterProvider;
    private Batch<Activity> activityBatch;
    private Batch<Metric> metricBatch;

    public enum ScopeLayout
    {
        /// <summary>Every item comes from one scope.</summary>
        SingleScope,

        /// <summary>Items come from 4 scopes with distinct names, interleaved.</summary>
        FewScopes,

        /// <summary>Items come from 64 scopes sharing one name and differing by version, interleaved.</summary>
        ManySameNameScopesInterleaved,
    }

    [Params(ScopeLayout.SingleScope, ScopeLayout.FewScopes, ScopeLayout.ManySameNameScopesInterleaved)]
    public ScopeLayout Layout { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.buffer = ProtobufSerializer.RentBuffer(1024 * 1024);

        var prefix = "benchmark.scope-grouping." + Guid.NewGuid().ToString("N");
        var scopeCount = this.Layout switch
        {
            ScopeLayout.SingleScope => 1,
            ScopeLayout.FewScopes => 4,
            _ => 64,
        };

        for (var i = 0; i < scopeCount; i++)
        {
            var name = this.Layout == ScopeLayout.FewScopes ? $"{prefix}.{i}" : prefix;
            var version = this.Layout == ScopeLayout.ManySameNameScopesInterleaved ? $"1.0.{i}" : "1.0.0";

            this.activitySources.Add(new ActivitySource(new ActivitySourceOptions(name) { Version = version }));
            this.meters.Add(new Meter(new MeterOptions(name) { Version = version }));
        }

        this.SetupActivities(prefix);
        this.SetupMetrics(prefix);
    }

    [Benchmark]
    public int WriteTraceData()
        => ProtobufOtlpTraceSerializer.WriteTraceData(ref this.buffer, 0, this.sdkLimitOptions, Resource.Empty, in this.activityBatch, out _);

    [Benchmark]
    public int WriteMetricsData()
        => ProtobufOtlpMetricSerializer.WriteMetricsData(ref this.buffer, 0, Resource.Empty, in this.metricBatch, out _);

    [GlobalCleanup]
    public void Cleanup()
    {
        ProtobufSerializer.ReturnBuffer(this.buffer);
        this.buffer = null!;
        this.meterProvider?.Dispose();
        this.activityListener?.Dispose();

        foreach (var meter in this.meters)
        {
            meter.Dispose();
        }

        foreach (var activitySource in this.activitySources)
        {
            activitySource.Dispose();
        }
    }

    private void SetupActivities(string prefix)
    {
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(prefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.activityListener);

        var activities = new Activity[ItemCount];
        for (var i = 0; i < ItemCount; i++)
        {
            var activity = this.activitySources[i % this.activitySources.Count].StartActivity("span")!;
            activity.Stop();
            activities[i] = activity;
        }

        this.activityBatch = new Batch<Activity>(activities, activities.Length);
    }

    private void SetupMetrics(string prefix)
    {
        var collected = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(prefix + "*")
            .AddInMemoryExporter(collected)
            .Build();

        for (var i = 0; i < ItemCount; i++)
        {
            this.meters[i % this.meters.Count].CreateCounter<long>("counter." + i).Add(1);
        }

        this.meterProvider.ForceFlush();

        this.metricBatch = new Batch<Metric>([.. collected], collected.Count);
    }
}
