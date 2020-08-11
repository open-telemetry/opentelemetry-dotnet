// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    public static void Main()
    {
        var summary = BenchmarkRunner.Run<TraceBenchmark>();
    }

    [MemoryDiagnoser]
    public class TraceBenchmark
    {
        private readonly ActivitySource sourceWithNoListener = new ActivitySource("Benchmark.NoListener");
        private readonly ActivitySource sourceWithPropagationDataListner = new ActivitySource("Benchmark.PropagationDataListner");
        private readonly ActivitySource sourceWithAllDataListner = new ActivitySource("Benchmark.AllDataListner");
        private readonly ActivitySource sourceWithAllDataAndRecordedListner = new ActivitySource("Benchmark.AllDataAndRecordedListner");
        private readonly ActivitySource sourceWithOneProcessor = new ActivitySource("Benchmark.OneProcessor");
        private readonly ActivitySource sourceWithTwoProcessors = new ActivitySource("Benchmark.TwoProcessors");
        private readonly ActivitySource sourceWithThreeProcessors = new ActivitySource("Benchmark.ThreeProcessors");

        public TraceBenchmark()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithPropagationDataListner.Name,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.PropagationData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.PropagationData,
            });

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataListner.Name,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            });

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == this.sourceWithAllDataAndRecordedListner.Name,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllDataAndRecorded,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllDataAndRecorded,
            });

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(null)
                .AddSource(this.sourceWithOneProcessor.Name)
                .AddProcessor(new DummyActivityProcessor())
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(null)
                .AddSource(this.sourceWithTwoProcessors.Name)
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(null)
                .AddSource(this.sourceWithThreeProcessors.Name)
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .AddProcessor(new DummyActivityProcessor())
                .Build();
        }

        [Benchmark]
        public void NoListener()
        {
            using (var activity = this.sourceWithNoListener.StartActivity("Benchmark"))
            {
                // this activity won't be created as there is no listener
            }
        }

        [Benchmark]
        public void PropagationDataListner()
        {
            using (var activity = this.sourceWithPropagationDataListner.StartActivity("Benchmark"))
            {
                // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
            }
        }

        [Benchmark]
        public void AllDataListner()
        {
            using (var activity = this.sourceWithAllDataListner.StartActivity("Benchmark"))
            {
                // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
            }
        }

        [Benchmark]
        public void AllDataAndRecordedListner()
        {
            using (var activity = this.sourceWithAllDataAndRecordedListner.StartActivity("Benchmark"))
            {
                // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
            }
        }

        [Benchmark]
        public void OneProcessor()
        {
            using (var activity = this.sourceWithOneProcessor.StartActivity("Benchmark"))
            {
            }
        }

        [Benchmark]
        public void TwoProcessors()
        {
            using (var activity = this.sourceWithTwoProcessors.StartActivity("Benchmark"))
            {
            }
        }

        [Benchmark]
        public void ThreeProcessors()
        {
            using (var activity = this.sourceWithThreeProcessors.StartActivity("Benchmark"))
            {
            }
        }
    }

    internal class DummyActivityProcessor : ActivityProcessor
    {
    }
}
