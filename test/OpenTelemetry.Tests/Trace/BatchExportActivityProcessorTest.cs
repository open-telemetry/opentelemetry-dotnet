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

using System.Diagnostics;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class BatchExportActivityProcessorTest
    {
        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchActivityExportProcessor(null));
        }

        [Fact]
        public void CheckConstructorWithInvalidValues()
        {
            var exportedItems = new List<Activity>();
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxQueueSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxExportBatchSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), maxQueueSize: 1, maxExportBatchSize: 2049));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), scheduledDelayMilliseconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems), exporterTimeoutMilliseconds: -1));
        }

        [Fact]
        public void CheckIfBatchIsExportingOnQueueLimit_Coyote()
        {
            var config = Configuration.Create();
            var test = TestingEngine.Create(config, this.CheckIfBatchIsExportingOnQueueLimit);

            test.Run();
            Console.WriteLine(test.GetReport());
            Console.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

            var dir = Directory.GetCurrentDirectory();

            if (test.TryEmitReports(dir, "CheckIfBatchIsExportingOnQueueLimit_Coyote", out IEnumerable<string> reportPaths))
            {
                foreach (var reportPath in reportPaths)
                {
                    Console.WriteLine($"Execution Report: {reportPath}");
                }
            }

            if (test.TryEmitCoverageReports(dir, "CheckIfBatchIsExportingOnQueueLimit_Coyote", out reportPaths))
            {
                foreach (var reportPath in reportPaths)
                {
                    Console.WriteLine($"Coverage report: {reportPath}");
                }
            }

            Assert.Equal(0, test.TestReport.NumOfFoundBugs);
        }

        [Fact]
        public void CheckIfBatchIsExportingOnQueueLimit()
        {
            var exportedItems1 = new List<Activity>();
            var exportedItems2 = new List<Activity>();

            using var activity = new Activity("start")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,
            };

            using var exporter1 = new InMemoryExporter<Activity>(exportedItems1);
            using var processor1 = new BatchActivityExportProcessor(
                exporter1,
                maxQueueSize: 1,
                maxExportBatchSize: 1,
                scheduledDelayMilliseconds: 100_000);

            using var exporter2 = new InMemoryExporter<Activity>(exportedItems2);
            using var processor2 = new BatchActivityExportProcessor(
                exporter2,
                maxQueueSize: 1,
                maxExportBatchSize: 1,
                scheduledDelayMilliseconds: 100_000);

            var tasks = new List<Task>()
            {
                Task.Run(
                    () =>
                    {
                        processor1.OnEnd(activity);
                        Thread.Sleep(2500);
                        processor1.ForceFlush();
                        Thread.Sleep(2500);
                        processor1.Shutdown();
                    }),

                Task.Run(
                    () =>
                    {
                        processor2.OnEnd(activity);
                        Thread.Sleep(2500);
                        processor2.ForceFlush();
                        Thread.Sleep(2500);
                        processor2.Shutdown();
                    }),
            };

            Task.WaitAll(tasks.ToArray());
            Assert.Equal(1, processor1.ShutdownDrainTarget);
            Assert.Single(exportedItems1);
            Assert.Equal(1, processor1.ProcessedCount);
            Assert.Equal(1, processor1.ReceivedCount);
            Assert.Equal(0, processor1.DroppedCount);

            Assert.Equal(1, processor2.ShutdownDrainTarget);
            Assert.Single(exportedItems2);
            Assert.Equal(1, processor2.ProcessedCount);
            Assert.Equal(1, processor2.ReceivedCount);
            Assert.Equal(0, processor2.DroppedCount);
        }

        [Fact]
        public void CheckForceFlushWithInvalidTimeout()
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new BatchActivityExportProcessor(exporter, maxQueueSize: 2, maxExportBatchSize: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => processor.ForceFlush(-2));
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new BatchActivityExportProcessor(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            using var activity1 = new Activity("start1")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,
            };

            using var activity2 = new Activity("start2")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,
            };

            processor.OnEnd(activity1);
            processor.OnEnd(activity2);

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
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new BatchActivityExportProcessor(
                exporter,
                maxQueueSize: 3,
                maxExportBatchSize: 3,
                exporterTimeoutMilliseconds: 30000);

            using var activity = new Activity("start")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,
            };

            processor.OnEnd(activity);
            processor.Shutdown(timeout);

            if (timeout == 0)
            {
                // Shutdown(0) will trigger flush and return immediately, so let's sleep for a while
                Thread.Sleep(1_000);
            }

            Assert.Single(exportedItems);

            Assert.Equal(1, processor.ProcessedCount);
            Assert.Equal(1, processor.ReceivedCount);
            Assert.Equal(0, processor.DroppedCount);
        }

        [Fact]
        public void CheckExportForRecordingButNotSampledActivity()
        {
            var exportedItems = new List<Activity>();
            using var exporter = new InMemoryExporter<Activity>(exportedItems);
            using var processor = new BatchActivityExportProcessor(
                exporter,
                maxQueueSize: 1,
                maxExportBatchSize: 1);

            using var activity = new Activity("start")
            {
                ActivityTraceFlags = ActivityTraceFlags.None,
            };

            processor.OnEnd(activity);
            processor.Shutdown();

            Assert.Empty(exportedItems);
            Assert.Equal(0, processor.ProcessedCount);
        }

        [Fact]
        public void CheckExportDrainsBatchOnFailure()
        {
            using var processor = new BatchActivityExportProcessor(
                exporter: new FailureExporter<Activity>(),
                maxQueueSize: 3,
                maxExportBatchSize: 3);

            using var activity = new Activity("start")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,
            };

            processor.OnEnd(activity);
            processor.OnEnd(activity);
            processor.OnEnd(activity);
            processor.Shutdown();

            Assert.Equal(3, processor.ProcessedCount); // Verify batch was drained even though nothing was exported.
        }

        private class FailureExporter<T> : BaseExporter<T>
            where T : class
        {
            public override ExportResult Export(in Batch<T> batch) => ExportResult.Failure;
        }
    }
}
