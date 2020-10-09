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
            Assert.Throws<ArgumentNullException>(() => new SimpleExportProcessor<Activity>(null));
        }

        [Fact]
        public void CheckExportedOnEnd()
        {
            var exported = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exported });
            using var processor = new SimpleExportProcessor<Activity>(exporter);

            processor.OnEnd(new Activity("start1"));
            Assert.Single(exported);

            processor.OnEnd(new Activity("start2"));
            Assert.Equal(2, exported.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            var exported = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exported });
            using var processor = new SimpleExportProcessor<Activity>(exporter);

            processor.OnEnd(new Activity("start1"));
            processor.OnEnd(new Activity("start2"));

            // checking before force flush
            Assert.Equal(2, exported.Count);

            // forcing flush
            processor.ForceFlush(timeout);
            Assert.Equal(2, exported.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckShutdownExport(int timeout)
        {
            var exported = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exported });
            using var processor = new SimpleExportProcessor<Activity>(exporter);

            processor.OnEnd(new Activity("start"));

            // checking before shutdown
            Assert.Single(exported);

            processor.Shutdown(timeout);
            Assert.Single(exported);
        }
    }
}
