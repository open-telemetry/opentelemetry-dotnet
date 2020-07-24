// <copyright file="BatchingActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class BatchingActivityProcessorTests : IDisposable
    {
        private const string ActivityName1 = "MyActivityName/1";
        private const string ActivityName2 = "MyActivityName/2";
        private const string ActivitySourceName = "my.source";

        private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);
        private static readonly ActivitySource Source = new ActivitySource(ActivitySourceName);

        [Fact]
        public void ThrowsOnInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchingActivityProcessor(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 0, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 2048, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 512, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 513));
        }

        [Fact]
        public async Task ShutdownTwice()
        {
            using var activityProcessor = new BatchingActivityProcessor(new TestActivityExporter(null));
            await activityProcessor.ShutdownAsync(CancellationToken.None);

            // does not throw
            await activityProcessor.ShutdownAsync(CancellationToken.None);
        }

        [Fact]
        public void DisposeTwice()
        {
            using var activityProcessor = new BatchingActivityProcessor(new TestActivityExporter(null));

            activityProcessor.Dispose();

            // does not throw
            activityProcessor.Dispose();
        }

        [Fact]
        public async Task ShutdownWithHugeScheduledDelay()
        {
            using var activityProcessor =
                new BatchingActivityProcessor(new TestActivityExporter(null), 128, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(100), 32);
            var sw = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                cts.Token.ThrowIfCancellationRequested();
                await activityProcessor.ShutdownAsync(cts.Token).ConfigureAwait(false);
            }

            sw.Stop();
            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void CancelWithExporterTimeoutMilliseconds()
        {
            using var inMemoryEventListener = new InMemoryEventListener();
            var activityExporter = new TestActivityExporter(null, sleepMilliseconds: 5000);
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(0), 1);
            using (var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                            .AddActivitySource(ActivitySourceName)
                            .SetSampler(new AlwaysOnSampler())
                            .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))))
            {
                var activity1 = this.CreateActivity(ActivityName1);
            } // Force everything to flush out of the processor.

            Assert.Contains(inMemoryEventListener.Events, (e) => e.EventId == 23);
        }

        [Fact]
        public void ExportDifferentSampledActivities()
        {
            var activityExporter = new TestActivityExporter(null);
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, 128);
            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                            .AddActivitySource(ActivitySourceName)
                            .SetSampler(new AlwaysOnSampler())
                            .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activity1 = this.CreateActivity(ActivityName1);
            var activity2 = this.CreateActivity(ActivityName2);

            var exported = this.WaitForActivities(activityExporter, 2, DefaultTimeout);

            Assert.Equal(2, exported.Length);
            Assert.Contains(activity1, exported);
            Assert.Contains(activity2, exported);
        }

        [Fact]
        public void ExporterIsSlowerThanDelay()
        {
            var exportStartTimes = new List<long>();
            var exportEndTimes = new List<long>();
            var activityExporter = new TestActivityExporter(_ =>
            {
                exportStartTimes.Add(Stopwatch.GetTimestamp());
                Thread.Sleep(50);
                exportEndTimes.Add(Stopwatch.GetTimestamp());
            });

            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(30), DefaultTimeout, 10);
            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                .AddActivitySource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activities = new List<Activity>();
            for (int i = 0; i < 20; i++)
            {
                activities.Add(this.CreateActivity(i.ToString()));
            }

            var exported = this.WaitForActivities(activityExporter, 20, TimeSpan.FromSeconds(2));

            Assert.Equal(activities.Count, exported.Length);
            Assert.InRange(exportStartTimes.Count, 2, 20);

            for (int i = 1; i < exportStartTimes.Count - 1; i++)
            {
                Assert.InRange(exportStartTimes[i], exportEndTimes[i - 1] + 1, exportStartTimes[i + 1] - 1);
            }
        }

        [Fact]
        public void AddActivityAfterQueueIsExhausted()
        {
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ =>
            {
                Interlocked.Increment(ref exportCalledCount);
                Thread.Sleep(50);
            });
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 1, TimeSpan.FromMilliseconds(100), DefaultTimeout, 1);
            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                .AddActivitySource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activities = new List<Activity>();
            for (int i = 0; i < 20; i++)
            {
                activities.Add(this.CreateActivity(i.ToString()));
            }

            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);

            Assert.Equal(1, exportCalledCount);
            Assert.InRange(exported.Length, 1, 2);
            Assert.Contains(activities.First(), exported);
        }

        [Fact]
        public void ExportMoreActivitiesThanTheMaxBatchSize()
        {
            var exporterCalled = new ManualResetEvent(false);
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ =>
            {
                exporterCalled.Set();
                Interlocked.Increment(ref exportCalledCount);
            });

            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, 3);
            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                .AddActivitySource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activity1 = this.CreateActivity(ActivityName1);
            var activity2 = this.CreateActivity(ActivityName1);
            var activity3 = this.CreateActivity(ActivityName1);
            var activity4 = this.CreateActivity(ActivityName1);
            var activity5 = this.CreateActivity(ActivityName1);
            var activity6 = this.CreateActivity(ActivityName1);

            // wait for exporter to be called to stabilize tests on the build server
            exporterCalled.WaitOne(TimeSpan.FromSeconds(10));

            var exported = this.WaitForActivities(activityExporter, 6, DefaultTimeout);

            Assert.InRange(exportCalledCount, 2, 6);

            Assert.Equal(6, exported.Count());
            Assert.Contains(activity1, exported);
            Assert.Contains(activity2, exported);
            Assert.Contains(activity3, exported);
            Assert.Contains(activity4, exported);
            Assert.Contains(activity5, exported);
            Assert.Contains(activity6, exported);
        }

        [Fact]
        public void ExportNotSampledActivities()
        {
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, 1);
            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                                    .SetSampler(new ParentOrElseSampler(new AlwaysOffSampler()))
                                    .AddActivitySource(ActivitySourceName)
                                    .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activity1 = this.CreateSampledEndedActivity(ActivityName1);
            var activity2 = this.CreateNotSampledEndedActivity(ActivityName2);

            // Activities are recorded and exported in the same order as they are created, we test that a non
            // sampled activity is not exported by creating and ending a sampled activity after a non sampled activity
            // and checking that the first exported activity is the sampled activity (the non sampled did not get
            // exported).
            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);
            Assert.Equal(1, exportCalledCount);

            // Need to check this because otherwise the variable activity1 is unused, other option is to not
            // have a activity1 variable.
            Assert.Single(exported);
            Assert.Contains(activity1, exported);
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        {
            var resetEvent = new ManualResetEvent(false);
            var activityExporter = new TestActivityExporter(_ => resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, 128);

            using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                .AddActivitySource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor)));

            var activity = Source.StartActivity("foo");

            // does not block
            var sw = Stopwatch.StartNew();
            activity?.Stop();
            sw.Stop();

            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            resetEvent.Set();

            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);

            Assert.Single(exported);
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueFullFlush()
        {
            const int batchSize = 75;
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor =
                new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, batchSize);
            using (var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                                         .AddActivitySource(ActivitySourceName)
                                         .SetSampler(new AlwaysOnSampler())
                                         .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))))
            {
                using var inMemoryEventListener = new InMemoryEventListener();
                var activities = new List<Activity>();
                for (int i = 0; i < 100; i++)
                {
                    activities.Add(this.CreateActivity(i.ToString()));
                }

                Assert.True(activityExporter.ExportedActivities.Length < activities.Count);
                using (var cts = new CancellationTokenSource(DefaultTimeout))
                {
                    await activityProcessor.ShutdownAsync(cts.Token);
                }

                // Get the shutdown event.
                // 2 is the EventId for OpenTelemetrySdkEventSource.ShutdownEvent
                // TODO: Expose event ids as internal, so tests can access them more reliably.
                var shutdownEvent = inMemoryEventListener.Events.Where((e) => e.EventId == 2).First();

                int droppedCount = 0;
                if (shutdownEvent != null)
                {
                    // There is a single payload which is the number of items left in buffer at shutdown.
                    droppedCount = (int)shutdownEvent.Payload[0];
                }

                Assert.True(activityExporter.WasShutDown);
                Assert.Equal(activities.Count, droppedCount + activityExporter.ExportedActivities.Length);
                Assert.InRange(exportCalledCount, activities.Count / batchSize, activities.Count);
            }
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueNotFullFlush()
        {
            const int batchSize = 25;
            int exportCalledCount = 0;

            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount), 30000);

            using var activityProcessor =
                new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, batchSize);
            using (var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                                     .AddActivitySource(ActivitySourceName)
                                     .SetSampler(new AlwaysOnSampler())
                                     .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))))
            {
                var activities = new List<Activity>();
                for (int i = 0; i < 100; i++)
                {
                    activities.Add(this.CreateActivity(i.ToString()));
                }

                Assert.True(activityExporter.ExportedActivities.Length < activities.Count);

                // we won't be able to export all before cancellation will fire
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
                {
                    bool canceled;
                    try
                    {
                        await activityProcessor.ShutdownAsync(cts.Token);
                        canceled = false;
                    }
                    catch (OperationCanceledException)
                    {
                        canceled = true;
                    }

                    Assert.True(canceled);
                }

                var exportedCount = activityExporter.ExportedActivities.Length;
                Assert.True(exportedCount < activities.Count);
            }
        }

        [Fact]
        public async Task ForceFlushExportsAllData()
        {
            const int batchSize = 75;
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, batchSize);
            using (var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                .AddActivitySource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))))
            {
                using var inMemoryEventListener = new InMemoryEventListener();
                var activities = new List<Activity>();
                for (int i = 0; i < 100; i++)
                {
                    activities.Add(this.CreateActivity(i.ToString()));
                }

                Assert.True(activityExporter.ExportedActivities.Length < activities.Count);
                using (var cts = new CancellationTokenSource(DefaultTimeout))
                {
                    await activityProcessor.ForceFlushAsync(cts.Token);
                }

                // Get the shutdown event.
                // 22 is the EventId for OpenTelemetrySdkEventSource.ForceFlushCompleted
                // TODO: Expose event ids as internal, so tests can access them more reliably.
                var flushEvent = inMemoryEventListener.Events.Where((e) => e.EventId == 22).First();

                int droppedCount = 0;
                if (flushEvent != null)
                {
                    // There is a single payload which is the number of items left in buffer at shutdown.
                    droppedCount = (int)flushEvent.Payload[0];
                }

                Assert.Equal(activities.Count, activityExporter.ExportedActivities.Length + droppedCount);
                Assert.InRange(exportCalledCount, activities.Count / batchSize, activities.Count);
            }
        }

        [Fact]
        public void DisposeFlushes()
        {
            const int batchSize = 1;
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount), 100);
            var activities = new List<Activity>();
            using var inMemoryEventListener = new InMemoryEventListener();
            using (var batchingActivityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, DefaultTimeout, batchSize))
            {
                using var openTelemetrySdk = Sdk.CreateTracerProvider(b => b
                                            .AddActivitySource(ActivitySourceName)
                                            .SetSampler(new AlwaysOnSampler())
                                            .AddProcessorPipeline(pp => pp.AddProcessor(ap => batchingActivityProcessor)));
                for (int i = 0; i < 3; i++)
                {
                    activities.Add(this.CreateActivity(i.ToString()));
                }

                Assert.True(activityExporter.ExportedActivities.Length < activities.Count);
            }

            // Get the shutdown event.
            // 2 is the EventId for OpenTelemetrySdkEventSource.ShutdownEvent
            // TODO: Expose event ids as internal, so tests can access them more reliably.
            var shutdownEvent = inMemoryEventListener.Events.Where((e) => e.EventId == 2).First();

            int droppedCount = 0;
            if (shutdownEvent != null)
            {
                // There is a single payload which is the number of items left in buffer at shutdown.
                droppedCount = (int)shutdownEvent.Payload[0];
            }

            Assert.True(activityExporter.WasShutDown);
            Assert.Equal(activities.Count, activityExporter.ExportedActivities.Length + droppedCount);
            Assert.Equal(activities.Count / batchSize, exportCalledCount);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private Activity CreateActivity(string activityName)
        {
            var activity = Source.StartActivity(activityName);
            activity?.Stop();
            return activity;
        }

        private Activity CreateSampledEndedActivity(string activityName)
        {
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var activity = Source.StartActivity(activityName, ActivityKind.Internal, context);
            activity.Stop();
            return activity;
        }

        private Activity CreateNotSampledEndedActivity(string activityName)
        {
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var activity = Source.StartActivity(activityName, ActivityKind.Server, context);
            activity?.Stop();
            return activity;
        }

        private Activity[] WaitForActivities(TestActivityExporter exporter, int activityCount, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (exporter.ExportedActivities.Length < activityCount && sw.Elapsed <= timeout)
            {
                Thread.Sleep(10);
            }

            Assert.True(
                exporter.ExportedActivities.Length >= activityCount,
                $"Expected at least {activityCount}, got {exporter.ExportedActivities.Length}");

            return exporter.ExportedActivities;
        }
    }
}
