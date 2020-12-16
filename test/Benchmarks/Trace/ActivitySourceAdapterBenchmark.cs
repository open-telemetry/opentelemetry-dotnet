// <copyright file="ActivitySourceAdapterBenchmark.cs" company="OpenTelemetry Authors">
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
    public class ActivitySourceAdapterBenchmark
    {
        private TestInstrumentation testInstrumentation = null;
        private TracerProvider tracerProvider;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddDiagnosticSourceInstrumentation((adapter) =>
                {
                    this.testInstrumentation = new TestInstrumentation(adapter);
                    return this.testInstrumentation;
                })
                .Build();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.tracerProvider.Dispose();
        }

        [Benchmark]
        public void ActivitySourceAdapterStartStop()
        {
            var activity = new Activity("test").Start();
            this.testInstrumentation.Start(activity);
            this.testInstrumentation.Stop(activity);
            activity.Stop();
        }

        private class TestInstrumentation
        {
            internal static ActivitySource ActivitySource = new ActivitySource("test", "1.0.0");
            private ActivitySourceAdapter adapter;

            public TestInstrumentation(ActivitySourceAdapter adapter)
            {
                this.adapter = adapter;
            }

            public void Start(Activity activity)
            {
                this.adapter.Start(activity, ActivityKind.Internal, ActivitySource);
            }

            public void Stop(Activity activity)
            {
                this.adapter.Stop(activity);
            }
        }
    }
}
