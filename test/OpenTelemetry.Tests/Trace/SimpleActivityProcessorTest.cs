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
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class SimpleActivityProcessorTest : IDisposable
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";
        private const string ActivitySourceName = "defaultactivitysource";

        [Fact]
        public void ThrowsOnNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleActivityProcessor(null));
        }

        [Fact]
        public void ThrowsInExporter()
        {
            var activityExporter = new TestActivityExporter(_ => throw new ArgumentException("123"));
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddSource("random")
                        .SetSampler(new AlwaysOnSampler())
                        .AddProcessor(new SimpleActivityProcessor(activityExporter))
                        .Build();

            ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");

            // does not throw
            activity.Stop();
        }

        [Fact]
        public void ProcessorDoesNotBlockOnExporter()
        {
            var activityExporter = new TestActivityExporter(async _ => await Task.Delay(500));
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddSource("random")
                        .AddProcessor(new SimpleActivityProcessor(activityExporter))
                        .Build();

            ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");

            // does not block
            var sw = Stopwatch.StartNew();
            activity.Stop();
            sw.Stop();

            Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            var exported = this.WaitForSpans(activityExporter, 1, TimeSpan.FromMilliseconds(600));

            Assert.Single(exported);
        }

        [Fact]
        public void ProcessorDoesNotSendRecordDecisionSpanToExporter()
        {
            var testSampler = new TestSampler();
            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.Record);
            };

            using var exporter = new TestActivityExporter(null);
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddSource("random")
                        .AddProcessor(new SimpleActivityProcessor(exporter))
                        .SetSampler(testSampler)
                        .Build();

            using ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");
            activity.Stop();

            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            Assert.False(activity.Recorded);

            var exported = this.WaitForSpans(exporter, 0, TimeSpan.FromMilliseconds(100));
            Assert.Empty(exported);
        }

        [Fact]
        public void ProcessorSendsRecordAndSampledDecisionSpanToExporter()
        {
            var testSampler = new TestSampler();
            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.RecordAndSampled);
            };

            using var exporter = new TestActivityExporter(null);
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddSource("random")
                        .AddProcessor(new SimpleActivityProcessor(exporter))
                        .SetSampler(testSampler)
                        .Build();

            using ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");
            activity.Stop();

            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
            Assert.True(activity.Recorded);

            var exported = this.WaitForSpans(exporter, 1, TimeSpan.FromMilliseconds(100));
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
            using var exporter = new TestActivityExporter(null);
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                                            .AddSource(ActivitySourceName)
                                            .AddProcessor(new SimpleActivityProcessor(exporter))
                                            .Build();

            var span1 = this.CreateSampledEndedSpan(SpanName1);
            var span2 = this.CreateSampledEndedSpan(SpanName2);

            var exported = this.WaitForSpans(exporter, 2, TimeSpan.FromMilliseconds(100));
            Assert.Equal(2, exported.Length);
            Assert.Contains(span1, exported);
            Assert.Contains(span2, exported);
        }

        [Fact]
        public void ExportNotSampledSpans()
        {
            using var exporter = new TestActivityExporter(null);
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                                            .AddSource(ActivitySourceName)
                                            .AddProcessor(new SimpleActivityProcessor(exporter))
                                            .Build();

            var span1 = this.CreateNotSampledEndedSpan(SpanName1);
            var span2 = this.CreateSampledEndedSpan(SpanName2);

            // Spans are recorded and exported in the same order as they are ended, we test that a non
            // sampled span is not exported by creating and ending a sampled span after a non sampled span
            // and checking that the first exported span is the sampled span (the non sampled did not get
            // exported).

            var exported = this.WaitForSpans(exporter, 1, TimeSpan.FromMilliseconds(100));

            // Need to check this because otherwise the variable span1 is unused, other option is to not
            // have a span1 variable.
            Assert.Single(exported);
            Assert.Contains(span2, exported);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private Activity CreateSampledEndedSpan(string spanName)
        {
            var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            using ActivitySource activitySource = new ActivitySource(ActivitySourceName);
            var activity = activitySource.StartActivity(spanName, ActivityKind.Internal, parentContext);
            activity?.Stop();
            return activity;
        }

        private Activity CreateNotSampledEndedSpan(string spanName)
        {
            var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            using ActivitySource activitySource = new ActivitySource(ActivitySourceName);
            var activity = activitySource.StartActivity(spanName, ActivityKind.Internal, parentContext);
            activity?.Stop();
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
