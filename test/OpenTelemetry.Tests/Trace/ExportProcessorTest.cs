// <copyright file="ExportProcessorTest.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class ExportProcessorTest
    {
        private const string ActivitySourceName = "ActivityExportProcessorTest";

        [Fact]
        public void ExportProcessorIgnoresActivityWhenDropped()
        {
            var sampler = new AlwaysOffSampler();
            var exportedItems = new List<Activity>();
            var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(sampler)
                .AddProcessor(processor)
                .Build();

            using (var activity = activitySource.StartActivity("Activity"))
            {
                Assert.False(activity.IsAllDataRequested);
                Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            }

            Assert.Empty(processor.ExportedItems);
        }

        [Fact]
        public void ExportProcessorIgnoresActivityMarkedAsRecordOnly()
        {
            var sampler = new RecordOnlySampler();
            var exportedItems = new List<Activity>();
            var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(sampler)
                .AddProcessor(processor)
                .Build();

            using (var activity = activitySource.StartActivity("Activity"))
            {
                Assert.True(activity.IsAllDataRequested);
                Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            }

            Assert.Empty(processor.ExportedItems);
        }

        [Fact]
        public void ExportProcessorExportsActivityMarkedAsRecordAndSample()
        {
            var sampler = new AlwaysOnSampler();
            var exportedItems = new List<Activity>();
            var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(sampler)
                .AddProcessor(processor)
                .Build();

            using (var activity = activitySource.StartActivity("Activity"))
            {
                Assert.True(activity.IsAllDataRequested);
                Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
            }

            Assert.Single(processor.ExportedItems);
        }
    }
}
