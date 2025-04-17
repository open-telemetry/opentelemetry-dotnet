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

public class TraceBenchmarks
{
    private readonly ActivitySource sourceWithNoListener = new("Benchmark.NoListener");
    private readonly ActivitySource sourceWithPropagationDataListner = new("Benchmark.PropagationDataListner");
    private readonly ActivitySource sourceWithAllDataListner = new("Benchmark.AllDataListner");
    private readonly ActivitySource sourceWithAllDataAndRecordedListner = new("Benchmark.AllDataAndRecordedListner");
    private readonly ActivitySource sourceWithOneProcessor = new("Benchmark.OneProcessor");
    private readonly ActivitySource sourceWithTwoProcessors = new("Benchmark.TwoProcessors");
    private readonly ActivitySource sourceWithThreeProcessors = new("Benchmark.ThreeProcessors");
    private readonly ActivitySource sourceWithOneLegacyActivityOperationNameSubscription = new("Benchmark.OneInstrumentation");
    private readonly ActivitySource sourceWithTwoLegacyActivityOperationNameSubscriptions = new("Benchmark.TwoInstrumentations");

    public TraceBenchmarks()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithPropagationDataListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.PropagationData,
        });

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        });

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataAndRecordedListner.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        });

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithOneProcessor.Name)
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithTwoProcessors.Name)
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithThreeProcessors.Name)
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithOneLegacyActivityOperationNameSubscription.Name)
            .AddLegacySource("TestOperationName")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.sourceWithTwoLegacyActivityOperationNameSubscriptions.Name)
            .AddLegacySource("TestOperationName1")
            .AddLegacySource("TestOperationName2")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddLegacySource("ExactMatch.OperationName1")
            .AddProcessor(new DummyActivityProcessor())
            .Build();

        Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddLegacySource("WildcardMatch.*")
            .AddProcessor(new DummyActivityProcessor())
            .Build();
    }

    [Benchmark]
    public void NoListener()
    {
        // this activity won't be created as there is no listener
        using var activity = this.sourceWithNoListener.StartActivity("Benchmark");
    }

    [Benchmark]
    public void PropagationDataListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithPropagationDataListner.StartActivity("Benchmark");
    }

    [Benchmark]
    public void AllDataListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithAllDataListner.StartActivity("Benchmark");
    }

    [Benchmark]
    public void AllDataAndRecordedListner()
    {
        // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        using var activity = this.sourceWithAllDataAndRecordedListner.StartActivity("Benchmark");
    }

    [Benchmark]
    public void OneProcessor()
    {
        using var activity = this.sourceWithOneProcessor.StartActivity("Benchmark");
    }

    [Benchmark]
    public void TwoProcessors()
    {
        using var activity = this.sourceWithTwoProcessors.StartActivity("Benchmark");
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        using var activity = this.sourceWithThreeProcessors.StartActivity("Benchmark");
    }

    [Benchmark]
    public void OneInstrumentation()
    {
        using var activity = this.sourceWithOneLegacyActivityOperationNameSubscription.StartActivity("Benchmark");
    }

    [Benchmark]
    public void TwoInstrumentations()
    {
        using var activity = this.sourceWithTwoLegacyActivityOperationNameSubscriptions.StartActivity("Benchmark");
    }

    [Benchmark]
    public void LegacyActivity_ExactMatchMode()
    {
        using var activity = new Activity("ExactMatch.OperationName1");
        activity.Start();
    }

    [Benchmark]
    public void LegacyActivity_WildcardMatchMode()
    {
        using var activity = new Activity("WildcardMatch.OperationName1");
        activity.Start();
    }

    internal class DummyActivityProcessor : BaseProcessor<Activity>
    {
    }
}
