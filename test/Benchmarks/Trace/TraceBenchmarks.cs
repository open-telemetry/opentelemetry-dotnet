// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method                           | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------------- |----------:|---------:|---------:|-------:|----------:|
| NoListener                       |  14.00 ns | 0.173 ns | 0.162 ns |      - |         - |
| PropagationDataListner           | 265.96 ns | 4.022 ns | 3.762 ns | 0.0663 |     416 B |
| AllDataListner                   | 255.14 ns | 1.819 ns | 1.702 ns | 0.0663 |     416 B |
| AllDataAndRecordedListner        | 258.32 ns | 2.387 ns | 2.116 ns | 0.0663 |     416 B |
| OneProcessor                     | 277.12 ns | 2.059 ns | 1.926 ns | 0.0663 |     416 B |
| TwoProcessors                    | 276.82 ns | 4.442 ns | 4.155 ns | 0.0663 |     416 B |
| ThreeProcessors                  | 283.12 ns | 1.970 ns | 1.645 ns | 0.0663 |     416 B |
| OneInstrumentation               | 281.13 ns | 2.199 ns | 2.057 ns | 0.0663 |     416 B |
| TwoInstrumentations              | 273.99 ns | 2.792 ns | 2.475 ns | 0.0663 |     416 B |
| LegacyActivity_ExactMatchMode    | 471.38 ns | 2.211 ns | 1.960 ns | 0.0658 |     416 B |
| LegacyActivity_WildcardMatchMode | 496.84 ns | 2.138 ns | 2.000 ns | 0.0658 |     416 B |
*/

namespace Benchmarks.Trace;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class TraceBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private ActivitySource? sourceWithNoListener;
    private ActivitySource? sourceWithPropagationDataListner;
    private ActivitySource? sourceWithAllDataListner;
    private ActivitySource? sourceWithAllDataAndRecordedListner;
    private ActivitySource? sourceWithOneProcessor;
    private ActivitySource? sourceWithTwoProcessors;
    private ActivitySource? sourceWithThreeProcessors;
    private ActivitySource? sourceWithOneLegacyActivityOperationNameSubscription;
    private ActivitySource? sourceWithTwoLegacyActivityOperationNameSubscriptions;

    private TracerProvider? tracerProvierWithOneProcessor;
    private TracerProvider? tracerProvierWithTwoProcessors;
    private TracerProvider? tracerProvierWithThreeProcessors;
    private TracerProvider? tracerProvierWithOneLegacyActivityOperationNameSubscription;
    private TracerProvider? tracerProvierWithTwoLegacyActivityOperationNameSubscriptions;
    private TracerProvider? tracerProvierWithExactMatchLegacyActivityListner;
    private TracerProvider? tracerProvierWithWildcardMatchLegacyActivityListner;

    private ActivityListener? activityListenerPropagationData;
    private ActivityListener? activityListenerAllData;
    private ActivityListener? activityListenerAllDataAndRecordedData;

    [GlobalSetup]
    public void Setup()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        this.sourceWithNoListener = new("Benchmark.NoListener");
        this.sourceWithPropagationDataListner = new("Benchmark.PropagationDataListner");
        this.sourceWithAllDataListner = new("Benchmark.AllDataListner");
        this.sourceWithAllDataAndRecordedListner = new("Benchmark.AllDataAndRecordedListner");
        this.sourceWithOneProcessor = new("Benchmark.OneProcessor");
        this.sourceWithTwoProcessors = new("Benchmark.TwoProcessors");
        this.sourceWithThreeProcessors = new("Benchmark.ThreeProcessors");
        this.sourceWithOneLegacyActivityOperationNameSubscription = new("Benchmark.OneInstrumentation");
        this.sourceWithTwoLegacyActivityOperationNameSubscriptions = new("Benchmark.TwoInstrumentations");

        this.activityListenerPropagationData = new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithPropagationDataListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.PropagationData,
        };

        ActivitySource.AddActivityListener(this.activityListenerPropagationData);

        this.activityListenerAllData = new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(this.activityListenerAllData);

        this.activityListenerAllDataAndRecordedData = new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataAndRecordedListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.activityListenerAllDataAndRecordedData);

        this.tracerProvierWithOneProcessor = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithOneProcessor.Name)
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithTwoProcessors = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithTwoProcessors.Name)
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithThreeProcessors = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithThreeProcessors.Name)
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithOneLegacyActivityOperationNameSubscription = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithOneLegacyActivityOperationNameSubscription.Name)
            .AddLegacySource("TestOperationName")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithTwoLegacyActivityOperationNameSubscriptions = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithTwoLegacyActivityOperationNameSubscriptions.Name)
            .AddLegacySource("TestOperationName1")
            .AddLegacySource("TestOperationName2")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithExactMatchLegacyActivityListner = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddLegacySource("ExactMatch.OperationName1")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        this.tracerProvierWithWildcardMatchLegacyActivityListner = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddLegacySource("WildcardMatch.*")
            .AddProcessor(new DummyActivityProcessor())
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.sourceWithNoListener?.Dispose();
        this.sourceWithPropagationDataListner?.Dispose();
        this.sourceWithAllDataListner?.Dispose();
        this.sourceWithAllDataAndRecordedListner?.Dispose();
        this.sourceWithOneProcessor?.Dispose();
        this.sourceWithTwoProcessors?.Dispose();
        this.sourceWithThreeProcessors?.Dispose();
        this.sourceWithOneLegacyActivityOperationNameSubscription?.Dispose();
        this.sourceWithTwoLegacyActivityOperationNameSubscriptions?.Dispose();

        this.tracerProvierWithOneProcessor?.Dispose();
        this.tracerProvierWithTwoProcessors?.Dispose();
        this.tracerProvierWithThreeProcessors?.Dispose();
        this.tracerProvierWithOneLegacyActivityOperationNameSubscription?.Dispose();
        this.tracerProvierWithTwoLegacyActivityOperationNameSubscriptions?.Dispose();
        this.tracerProvierWithExactMatchLegacyActivityListner?.Dispose();
        this.tracerProvierWithWildcardMatchLegacyActivityListner?.Dispose();

        this.activityListenerPropagationData?.Dispose();
        this.activityListenerAllData?.Dispose();
        this.activityListenerAllDataAndRecordedData?.Dispose();
    }

    [Benchmark]
    public void NoListener()
    {
        // this activity won't be created as there is no listener
        using var activity = this.sourceWithNoListener!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void PropagationDataListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithPropagationDataListner!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void AllDataListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithAllDataListner!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void AllDataAndRecordedListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithAllDataAndRecordedListner!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void OneProcessor()
    {
        using var activity = this.sourceWithOneProcessor!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void TwoProcessors()
    {
        using var activity = this.sourceWithTwoProcessors!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        using var activity = this.sourceWithThreeProcessors!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void OneInstrumentation()
    {
        using var activity = this.sourceWithOneLegacyActivityOperationNameSubscription!.StartActivity("Benchmark");
    }

    [Benchmark]
    public void TwoInstrumentations()
    {
        using var activity = this.sourceWithTwoLegacyActivityOperationNameSubscriptions!.StartActivity("Benchmark");
    }

    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void LegacyActivity_ExactMatchMode()
#pragma warning restore CA1822 // Mark members as static
    {
        using var activity = new Activity("ExactMatch.OperationName1");
        activity.Start();
    }

    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void LegacyActivity_WildcardMatchMode()
#pragma warning restore CA1822 // Mark members as static
    {
        using var activity = new Activity("WildcardMatch.OperationName1");
        activity.Start();
    }

    internal class DummyActivityProcessor : BaseProcessor<Activity>
    {
    }
}
