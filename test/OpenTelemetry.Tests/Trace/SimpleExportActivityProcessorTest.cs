// <copyright file="SimpleExportActivityProcessorTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class SimpleExportActivityProcessorTest
    {
        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleActivityExportProcessor(null));
        }

        [Fact]
        public void CheckExportedOnEnd()
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new SimpleActivityExportProcessor(exporter);

            var activity1 = new Activity("start1");
            activity1.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            processor.OnEnd(activity1);
            Assert.Single(exportedItems);

            var activity2 = new Activity("start2");
            activity2.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            processor.OnEnd(activity2);
            Assert.Equal(2, exportedItems.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new SimpleActivityExportProcessor(exporter);

            var activity1 = new Activity("start1");
            activity1.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            var activity2 = new Activity("start2");
            activity2.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            processor.OnEnd(activity1);
            processor.OnEnd(activity2);

            // checking before force flush
            Assert.Equal(2, exportedItems.Count);

            // forcing flush
            processor.ForceFlush(timeout);
            Assert.Equal(2, exportedItems.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckShutdownExport(int timeout)
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new SimpleActivityExportProcessor(exporter);

            var activity = new Activity("start");
            activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            processor.OnEnd(activity);

            // checking before shutdown
            Assert.Single(exportedItems);

            processor.Shutdown(timeout);
            Assert.Single(exportedItems);
        }

        [Fact]
        public void CheckExportForRecordingButNotSampledActivity()
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new SimpleActivityExportProcessor(exporter);

            var activity = new Activity("start");
            activity.ActivityTraceFlags = ActivityTraceFlags.None;

            processor.OnEnd(activity);
            Assert.Empty(exportedItems);
        }
    }
}
