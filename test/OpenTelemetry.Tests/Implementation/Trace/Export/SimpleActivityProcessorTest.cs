// <copyright file="SimpleActivityProcessorTest.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Trace.Export.Test
{
    public class SimpleActivityProcessorTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";
        private const string ActivitySourceName = "defaultactivitysource";

        private TestActivityExporter activityExporter;
        private OpenTelemetrySdk openTelemetry;
        private ActivitySource activitySource;

        public SimpleActivityProcessorTest()
        {
            this.activityExporter = new TestActivityExporter(null);
            this.openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(b => b
                        .AddActivitySource(ActivitySourceName)
                        .AddProcessorPipeline(p => p
                        .SetExporter(this.activityExporter)
                        .SetExportingProcessor(e => new SimpleActivityProcessor(e)))
                .SetSampler(new AlwaysOnSampler()));
            this.activitySource = new ActivitySource(ActivitySourceName);
        }

        [Fact]
        public void ThrowsOnNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleActivityProcessor(null));
        }

        [Fact]
        public void ThrowsInExporter()
        {
            this.activityExporter = new TestActivityExporter(_ => throw new ArgumentException("123"));
            this.openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(b => b
                        .AddActivitySource("cijo")
                        .AddProcessorPipeline(p => p
                        .SetExporter(this.activityExporter)
                        .SetExportingProcessor(e => new SimpleActivityProcessor(e))));

            ActivitySource source = new ActivitySource("cijo");
            var activity = source.StartActivity("somename");

            // does not throw
            activity.Stop();
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        {
            this.activityExporter = new TestActivityExporter(async _ => await Task.Delay(500));
            this.openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(b => b
                        .AddActivitySource("cijo")
                        .AddProcessorPipeline(p => p
                        .SetExporter(this.activityExporter)
                        .SetExportingProcessor(e => new SimpleActivityProcessor(e))));

            ActivitySource source = new ActivitySource("cijo");
            var activity = source.StartActivity("somename");

            // does not block
            var sw = Stopwatch.StartNew();
            activity.Stop();
            sw.Stop();

            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            var exported = this.WaitForSpans(this.activityExporter, 1, TimeSpan.FromMilliseconds(600));

            Assert.Single(exported);
        }

        [Fact]
        public async Task ShutdownTwice()
        {
            var activityProcessor = new SimpleActivityProcessor(new TestActivityExporter(null));

            await activityProcessor.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);

            // does not throw
            await activityProcessor.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task ForceFlushReturnsCompletedTask()
        {
            var activityProcessor = new SimpleActivityProcessor(new TestActivityExporter(null));

            var forceFlushTask = activityProcessor.ForceFlushAsync(CancellationToken.None);
            await forceFlushTask;

            Assert.True(forceFlushTask.IsCompleted);
        }

        [Fact]
        public void ExportDifferentSampledSpans()
        {
            var span1 = this.CreateSampledEndedSpan(SpanName1);
            var span2 = this.CreateSampledEndedSpan(SpanName2);

            var exported = this.WaitForSpans(this.activityExporter, 2, TimeSpan.FromMilliseconds(100));
            Assert.Equal(2, exported.Length);
            Assert.Contains(span1, exported);
            Assert.Contains(span2, exported);
        }

        [Fact(Skip = "Reenable once AlwaysParentActivitySampler is added")]
        public void ExportNotSampledSpans()
        {
            var span1 = this.CreateNotSampledEndedSpan(SpanName1);
            var span2 = this.CreateSampledEndedSpan(SpanName2);

            // Spans are recorded and exported in the same order as they are ended, we test that a non
            // sampled span is not exported by creating and ending a sampled span after a non sampled span
            // and checking that the first exported span is the sampled span (the non sampled did not get
            // exported).

            var exported = this.WaitForSpans(this.activityExporter, 1, TimeSpan.FromMilliseconds(100));

            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(span2, exported);
        }

        public void Dispose()
        {
            this.activityExporter.ShutdownAsync(CancellationToken.None);
            Activity.Current = null;
        }

        private Activity CreateSampledEndedSpan(string spanName)
        {
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var activity = this.activitySource.StartActivity(spanName, ActivityKind.Internal, context);
            activity.Stop();
            return activity;
        }

        private Activity CreateNotSampledEndedSpan(string spanName)
        {
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            var activity = this.activitySource.StartActivity(spanName, ActivityKind.Internal, context);
            activity.Stop();
            return activity;
        }

        private Activity[] WaitForSpans(TestActivityExporter exporter, int spanCount, TimeSpan timeout)
        {
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        Thread.Sleep(0);
                        return exporter.ExportedActivities.Length >= spanCount;
                    }, timeout + TimeSpan.FromMilliseconds(20)));

            return exporter.ExportedActivities;
        }
    }
}
