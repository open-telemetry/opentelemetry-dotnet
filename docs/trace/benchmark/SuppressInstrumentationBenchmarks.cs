// <copyright file="SuppressInstrumentationBenchmarks.cs" company="OpenTelemetry Authors">
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
using BenchmarkDotNet.Attributes;
using OpenTelemetry;

[MemoryDiagnoser]
public class SuppressInstrumentationBenchmarks
{
    private readonly ActivitySource sourceWithSuppressInstrumentation = new ActivitySource("Benchmark.SuppressInstrumentation");
    private readonly ActivitySource sourceWithNoneListener = new ActivitySource("Benchmark.NoneListener");
    private readonly ActivitySource sourceWithAllDataAndRecordedListner = new ActivitySource("Benchmark.AllDataAndRecordedListner");

    public SuppressInstrumentationBenchmarks()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithSuppressInstrumentation.Name,
            GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => { return Sdk.SuppressInstrumentation ? ActivityDataRequest.None : ActivityDataRequest.AllDataAndRecorded; },
            GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => { return Sdk.SuppressInstrumentation ? ActivityDataRequest.None : ActivityDataRequest.AllDataAndRecorded; },
        });

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithNoneListener.Name,
            GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.None,
            GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.None,
        });

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataAndRecordedListner.Name,
            GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllDataAndRecorded,
            GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllDataAndRecorded,
        });
    }

    [Benchmark]
    public void SuppressInstrumentationFalse()
    {
        using (var activity = this.sourceWithSuppressInstrumentation.StartActivity("Benchmark"))
        {
            // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        }
    }

    [Benchmark]
    public void SuppressInstrumentationTrue()
    {
        using (Sdk.SuppressInstrumentation.Begin())
        {
            using (var activity = this.sourceWithSuppressInstrumentation.StartActivity("Benchmark"))
            {
                // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
            }
        }
    }

    [Benchmark]
    public void SuppressInstrumentationTrueNested()
    {
        using (Sdk.SuppressInstrumentation.Begin())
        {
            using (Sdk.SuppressInstrumentation.Begin())
            {
                using (var activity = this.sourceWithSuppressInstrumentation.StartActivity("Benchmark"))
                {
                    // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
                }
            }
        }
    }

    [Benchmark]
    public void NoneListener()
    {
        using (var activity = this.sourceWithNoneListener.StartActivity("Benchmark"))
        {
            // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        }
    }

    [Benchmark]
    public void AllDataAndRecordedListener1()
    {
        using (var activity = this.sourceWithAllDataAndRecordedListner.StartActivity("Benchmark"))
        {
            // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        }
    }

    [Benchmark]
    public void AllDataAndRecordedListener2()
    {
        var b = Sdk.SuppressInstrumentation;
        using (var activity = this.sourceWithAllDataAndRecordedListner.StartActivity("Benchmark"))
        {
            // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
        }
    }
}
