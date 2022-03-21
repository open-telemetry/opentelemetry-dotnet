// <copyright file="TraceBenchmarks.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
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
            using var activity = new Activity("ExactMatch.OperationName1").Start();
        }

        [Benchmark]
        public void LegacyActivity_WildcardMatchMode()
        {
            using var activity = new Activity("WildcardMatch.OperationName1").Start();
        }

        internal class DummyActivityProcessor : BaseProcessor<Activity>
        {
        }
    }
}
