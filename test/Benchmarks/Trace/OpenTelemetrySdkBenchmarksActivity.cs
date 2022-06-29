// <copyright file="OpenTelemetrySdkBenchmarksActivity.cs" company="OpenTelemetry Authors">
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
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace
{
    public class OpenTelemetrySdkBenchmarksActivity
    {
        private readonly ActivitySource benchmarkSource = new("Benchmark");
        private readonly ActivityContext parentCtx = new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        private readonly string parentId = $"00-{ActivityTraceId.CreateRandom()}.{ActivitySpanId.CreateRandom()}.00";
        private TracerProvider tracerProvider;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("BenchMark")
                .Build();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.tracerProvider.Dispose();
            this.benchmarkSource.Dispose();
        }

        [Benchmark]
        public void CreateActivity_NoopProcessor() => ActivityCreationScenarios.CreateActivity(this.benchmarkSource);

        [Benchmark]
        public void CreateActivity_WithParentContext_NoopProcessor() => ActivityCreationScenarios.CreateActivityFromParentContext(this.benchmarkSource, this.parentCtx);

        [Benchmark]
        public void CreateActivity_WithParentId_NoopProcessor() => ActivityCreationScenarios.CreateActivityFromParentId(this.benchmarkSource, this.parentId);

        [Benchmark]
        public void CreateActivity_WithAttributes_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAttributes(this.benchmarkSource);

        [Benchmark]
        public void CreateActivity_WithAttributesAndCustomProp_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithAttributesAndCustomProperty(this.benchmarkSource);

        [Benchmark]
        public void CreateActiviti_WithKind_NoopProcessor() => ActivityCreationScenarios.CreateActivityWithKind(this.benchmarkSource);
    }
}
