// <copyright file="BatchingActivitiyProcessorTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Trace.Export.Test
{
    public class BatchingActivitiyProcessorTest : IDisposable
    {
        private const string ActivityName1 = "MySpanName/1";
        private const string ActivityName2 = "MySpanName/2";

        private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        private Activity CreateSampledEndedActivity(string activityName, ActivityProcessor activityProcessor)
        {
            var source = new ActivitySource("my.source");
            var builder = new OpenTelemetryBuilder()
                .SetSampler(new AlwaysOnActivitySampler())
                .SetProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))
                .AddActivitySource(source.Name);

            // var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = source.StartActivity(activityName);
            activity?.Stop();
            return activity;
        }

        private Activity CreateNotSampledEndedActivity(string activityName, ActivityProcessor activityProcessor)
        {
            var source = new ActivitySource("my.source");
            var builder = new OpenTelemetryBuilder()
                .SetSampler(new AlwaysOnActivitySampler())
                .SetProcessorPipeline(pp => pp.AddProcessor(ap => activityProcessor))
                .AddActivitySource(source.Name);

            // var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            var activity = source.StartActivity(activityName);
            activity?.Stop();
            return activity;
        }

        [Fact]
        public void ThrowsOnInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchingActivityProcessor(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 0, TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 2048, TimeSpan.FromSeconds(5), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingActivityProcessor(new TestActivityExporter(null), 512, TimeSpan.FromSeconds(5), 513));
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
        public async Task ShutdownWithHugeScheduleDelay()
        {
            using var activityProcessor =
                new BatchingActivityProcessor(new TestActivityExporter(null), 128, TimeSpan.FromMinutes(1), 32);
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
        public void ExportDifferentSampledActivities()
        {
            var activityExporter = new TestActivityExporter(null);
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, 128);
            var activity1 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var activity2 = this.CreateSampledEndedActivity(ActivityName2, activityProcessor);

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

            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(30), 2);
            var activities = new List<Activity>();
            for (int i = 0; i < 20; i++)
            {
                activities.Add(this.CreateSampledEndedActivity(i.ToString(), activityProcessor));
            }

            var exported = this.WaitForActivities(activityExporter, 20, TimeSpan.FromSeconds(2));

            Assert.Equal(activities.Count, exported.Length);
            Assert.InRange(exportStartTimes.Count, 10, 20);

            for (int i = 1; i < exportStartTimes.Count - 1; i++)
            {
                Assert.InRange(exportStartTimes[i], exportEndTimes[i - 1] + 1, exportStartTimes[i + 1] - 1);
            }
        }

        [Fact]
        public void AddSpanAfterQueueIsExhausted()
        {
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 1, TimeSpan.FromMilliseconds(100), 1);
            var activities = new List<Activity>();
            for (int i = 0; i < 20; i++)
            {
                activities.Add(this.CreateSampledEndedActivity(i.ToString(), activityProcessor));
            }

            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);

            Assert.Equal(1, exportCalledCount);
            Assert.InRange(exported.Length, 1, 2);
            Assert.Contains(activities.First(), exported);
        }

        [Fact]
        public void ExportMoreSpansThanTheMaxBatchSize()
        {
            var exporterCalled = new ManualResetEvent(false);
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ =>
            {
                exporterCalled.Set();
                Interlocked.Increment(ref exportCalledCount);
            });

            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, 3);
            var span1 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var span2 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var span3 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var span4 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var span5 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);
            var span6 = this.CreateSampledEndedActivity(ActivityName1, activityProcessor);

            // wait for exporter to be called to stabilize tests on the build server
            exporterCalled.WaitOne(TimeSpan.FromSeconds(10));

            var exported = this.WaitForActivities(activityExporter, 6, DefaultTimeout);

            Assert.InRange(exportCalledCount, 2, 6);

            Assert.Equal(6, exported.Count());
            Assert.Contains(span1, exported);
            Assert.Contains(span2, exported);
            Assert.Contains(span3, exported);
            Assert.Contains(span4, exported);
            Assert.Contains(span5, exported);
            Assert.Contains(span6, exported);
        }


        [Fact]
        public void ExportNotSampledActivities()
        {
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor = new BatchingActivityProcessor(activityExporter, 128, DefaultDelay, 3);
            var activity1 = this.CreateNotSampledEndedActivity(ActivityName1, activityProcessor);
            var activity2 = this.CreateSampledEndedActivity(ActivityName2, activityProcessor);
            // Spans are recorded and exported in the same order as they are ended, we test that a non
            // sampled span is not exported by creating and ending a sampled span after a non sampled span
            // and checking that the first exported span is the sampled span (the non sampled did not get
            // exported).
            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);
            Assert.Equal(1, exportCalledCount);

            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(activity2, exported);
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        {
            var resetEvent = new ManualResetEvent(false);
            var activityExporter = new TestActivityExporter(_ => resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            var builder = new OpenTelemetryBuilder()
                .SetProcessorPipeline(pp => pp
                    .SetExporter(activityExporter)
                    .SetExportingProcessor(ae => new BatchingActivityProcessor(ae, 128, DefaultDelay, 128)))
                .AddActivitySource("test.source");

            // var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var source = new ActivitySource("test.source");
            var activity = source.StartActivity("foo");

            // does not block
            var sw = Stopwatch.StartNew();
            activity.Stop();
            sw.Stop();

            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            resetEvent.Set();

            var exported = this.WaitForActivities(activityExporter, 1, DefaultTimeout);

            Assert.Single(exported);
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueFullFlush()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            using var activityProcessor =
                new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(100), batchSize);
            var activities = new List<Activity>();
            for (int i = 0; i < 100; i++)
            {
                activities.Add(this.CreateSampledEndedActivity(i.ToString(), activityProcessor));
            }

            Assert.True(activityExporter.ExportedActivities.Length < activities.Count);
            using (var cts = new CancellationTokenSource(DefaultTimeout))
            {
                await activityProcessor.ShutdownAsync(cts.Token);
            }

            Assert.True(activityExporter.WasShutDown);
            Assert.Equal(activities.Count, activityExporter.ExportedActivities.Length);
            Assert.InRange(exportCalledCount, activities.Count / batchSize, activities.Count);
        }

        [Fact]
        public async Task ShutdownOnNotEmptyQueueNotFullFlush()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;

            // we'll need about 1.5 sec to export all spans
            // we export 100 spans in batches of 2, each export takes 30ms, in one thread
            var activityExporter = new TestActivityExporter(_ =>
            {
                Interlocked.Increment(ref exportCalledCount);
                Thread.Sleep(30);
            });

            using var activityProcessor =
                new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(100), batchSize);
            var activities = new List<Activity>();
            for (int i = 0; i < 100; i++)
            {
                activities.Add(this.CreateSampledEndedActivity(i.ToString(), activityProcessor));
            }

            Assert.True(activityExporter.ExportedActivities.Length < activities.Count);

            // we won't bs able to export all before cancellation will fire
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            {
                await activityProcessor.ShutdownAsync(cts.Token);
            }

            var exportedCount = activityExporter.ExportedActivities.Length;
            Assert.True(exportedCount < activities.Count);
        }

        [Fact]
        public void DisposeFlushes()
        {
            const int batchSize = 2;
            int exportCalledCount = 0;
            var activityExporter = new TestActivityExporter(_ => Interlocked.Increment(ref exportCalledCount));
            var activities = new List<Activity>();
            using (var spanProcessor = new BatchingActivityProcessor(activityExporter, 128, TimeSpan.FromMilliseconds(100), batchSize))
            {
                for (int i = 0; i < 100; i++)
                {
                    activities.Add(CreateSampledEndedActivity(i.ToString(), spanProcessor));
                }
                Assert.True(activityExporter.ExportedActivities.Length < activities.Count);
            }
            Assert.True(activityExporter.WasShutDown);
            Assert.Equal(activities.Count, activityExporter.ExportedActivities.Length);
            Assert.Equal(activities.Count / batchSize, exportCalledCount);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private Activity[] WaitForActivities(TestActivityExporter exporter, int spanCount, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (exporter.ExportedActivities.Length < spanCount && sw.Elapsed <= timeout)
            {
                Thread.Sleep(10);
            }

            Assert.True(exporter.ExportedActivities.Length >= spanCount,
                $"Expected at least {spanCount}, got {exporter.ExportedActivities.Length}");

            return exporter.ExportedActivities;
        }
    }
}
