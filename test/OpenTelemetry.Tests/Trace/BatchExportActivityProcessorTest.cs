// <copyright file="BatchExportActivityProcessorTest.cs" company="OpenTelemetry Authors">
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
    public class BatchExportActivityProcessorTest
    {
        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchExportProcessor<Activity>(null));
        }

        [Fact]
        public void CheckConstructorWithInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportProcessor<Activity>(new InMemoryExporter<Activity>(), maxQueueSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportProcessor<Activity>(new InMemoryExporter<Activity>(), maxExportBatchSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportProcessor<Activity>(new InMemoryExporter<Activity>(), maxQueueSize: 1, maxExportBatchSize: 2049));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportProcessor<Activity>(new InMemoryExporter<Activity>(), scheduledDelayMilliseconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportProcessor<Activity>(new InMemoryExporter<Activity>(), exporterTimeoutMilliseconds: -1));
        }

        [Fact]
        public void CheckIfBatchIsExportingOnQueueLimit()
        {
            var exportedItems = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exportedItems });
            using var processor = new BatchExportProcessor<Activity>(
                exporter,
                maxQueueSize: 1,
                maxExportBatchSize: 1,
                scheduledDelayMilliseconds: 100_000);

            processor.OnEnd(new Activity("start"));

            for (int i = 0; i < 10 && exportedItems.Count == 0; i++)
            {
                Thread.Sleep(500);
            }

            Assert.Single(exportedItems);

            Assert.Equal(1, processor.ProcessedCount);
            Assert.Equal(1, processor.ReceivedCount);
            Assert.Equal(0, processor.DroppedCount);
        }

        [Fact]
        public void CheckForceFlushWithInvalidTimeout()
        {
            using var exporter = new InMemoryExporter<Activity>();
            using var processor = new BatchExportProcessor<Activity>(exporter, maxQueueSize: 2, maxExportBatchSize: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => processor.ForceFlush(-2));
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            var exportedItems = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exportedItems });
            using var processor = new BatchExportProcessor<Activity>(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            processor.OnEnd(new Activity("start1"));
            processor.OnEnd(new Activity("start2"));

            Assert.Equal(0, processor.ProcessedCount);

            // waiting to see if time is triggering the exporter
            Thread.Sleep(1_000);
            Assert.Empty(exportedItems);

            // forcing flush
            processor.ForceFlush(timeout);

            if (timeout == 0)
            {
                // ForceFlush(0) will trigger flush and return immediately, so let's sleep for a while
                Thread.Sleep(1_000);
            }

            Assert.Equal(2, exportedItems.Count);

            Assert.Equal(2, processor.ProcessedCount);
            Assert.Equal(2, processor.ReceivedCount);
            Assert.Equal(0, processor.DroppedCount);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckShutdownExport(int timeout)
        {
            var exportedItems = new List<object>();
            using var exporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { ExportedItems = exportedItems });
            using var processor = new BatchExportProcessor<Activity>(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            processor.OnEnd(new Activity("start"));
            processor.Shutdown(timeout);

            if (timeout == 0)
            {
                // ForceFlush(0) will trigger flush and return immediately, so let's sleep for a while
                Thread.Sleep(1_000);
            }

            Assert.Single(exportedItems);

            Assert.Equal(1, processor.ProcessedCount);
            Assert.Equal(1, processor.ReceivedCount);
            Assert.Equal(0, processor.DroppedCount);
        }
    }
}
