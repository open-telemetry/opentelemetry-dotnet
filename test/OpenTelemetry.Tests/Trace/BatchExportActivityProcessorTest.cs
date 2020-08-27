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
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Tests.Shared;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Trace
{
    public class BatchExportActivityProcessorTest
    {
        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchExportActivityProcessor(null));
        }

        [Fact]
        public void CheckConstructorWithInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportActivityProcessor(new TestActivityExporter(), maxQueueSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportActivityProcessor(new TestActivityExporter(), maxExportBatchSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportActivityProcessor(new TestActivityExporter(), maxExportBatchSize: 2049));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportActivityProcessor(new TestActivityExporter(), scheduledDelayMilliseconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportActivityProcessor(new TestActivityExporter(), exporterTimeoutMilliseconds: -1));
        }

        [Fact]
        public void CheckIfBatchIsExportingOnQueueLimit()
        {
            using var exporter = new TestActivityExporter();
            using var processor = new BatchExportActivityProcessor(exporter, maxQueueSize: 1, maxExportBatchSize: 1);
            processor.OnStart(new Activity("start"));
            processor.OnEnd(new Activity("start"));

            while (true)
            {
                if (exporter.Exported.Count == 0)
                {
                    Thread.Sleep(500);
                }
                else
                {
                    break;
                }
            }

            Assert.Single(exporter.Exported);
        }

        [Fact]
        public void CheckForceFlushWithInvalidTimeout()
        {
            using var exporter = new TestActivityExporter();
            using var processor = new BatchExportActivityProcessor(exporter, maxQueueSize: 2, maxExportBatchSize: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => processor.ForceFlush(-2));
        }

        [Fact]
        public void CheckForceFlushExport()
        {
            using var exporter = new TestActivityExporter();
            using var processor = new BatchExportActivityProcessor(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            processor.OnStart(new Activity("start"));
            processor.OnEnd(new Activity("start"));

            // waiting to see if time is triggerint the exporter
            Thread.Sleep(1_000);
            Assert.Empty(exporter.Exported);

            // forcing flush
            processor.ForceFlush();
            Assert.Single(exporter.Exported);
        }

        [Fact]
        public void CheckShutdownExport()
        {
            using var exporter = new TestActivityExporter();
            using var processor = new BatchExportActivityProcessor(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            processor.OnStart(new Activity("start"));
            processor.OnEnd(new Activity("start"));
            processor.Shutdown();
            Assert.Single(exporter.Exported);
        }
    }
}
