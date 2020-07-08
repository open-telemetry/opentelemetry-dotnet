// <copyright file="ActivitySourceAdapterTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OpenTelemetry.Tests.Implementation.Testing.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace
{
    public class ActivitySourceAdapterTest : IDisposable
    {
        private TestSampler testSampler;
        private TestActivityProcessor testProcessor;
        private Resource testResource = Resources.Resources.CreateServiceResource("test-resource");
        private ActivitySourceAdapter activitySourceAdapter;

        static ActivitySourceAdapterTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        public ActivitySourceAdapterTest()
        {
            this.testSampler = new TestSampler();
            this.testProcessor = new TestActivityProcessor();
            this.activitySourceAdapter = new ActivitySourceAdapter(this.testSampler, this.testProcessor, this.testResource);
        }

        [Fact]
        public void ActivitySourceAdapterSetsResource()
        {
            var activity = new Activity("test");
            activity.Start();
            this.activitySourceAdapter.Start(activity);
            activity.Stop();
            this.activitySourceAdapter.Stop(activity);

            Assert.Equal(this.testResource, activity.GetResource());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ActivitySourceAdapterCallsStartStopActivityProcessor(bool isSampled)
        {
            this.testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(isSampled);
            };

            bool startCalled = false;
            bool endCalled = false;
            this.testProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;

                    // If start is called, that means activity is sampled,
                    // and TraceFlag is set to Recorded.
                    Assert.Equal(ActivityTraceFlags.Recorded, a.ActivityTraceFlags);
                };

            this.testProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            var activity = new Activity("test");
            activity.Start();
            this.activitySourceAdapter.Start(activity);
            activity.Stop();
            this.activitySourceAdapter.Stop(activity);

            Assert.Equal(isSampled, startCalled);
            Assert.Equal(isSampled, endCalled);
        }

        [Fact]
        public void ActivitySourceAdapterPopulatesSamplingParamsCorrectlyForRootActivity()
        {
            this.testSampler.SamplingAction = (samplingParameters) =>
            {
                Assert.Equal(default, samplingParameters.ParentContext);
                return new SamplingResult(true);
            };

            // Start activity without setting parent. i.e it'll have null parent
            // and becomes root activity
            var activity = new Activity("test");
            activity.Start();
            this.activitySourceAdapter.Start(activity);
            activity.Stop();
            this.activitySourceAdapter.Stop(activity);
        }

        [Theory]
        [InlineData(ActivityTraceFlags.None)]
        [InlineData(ActivityTraceFlags.Recorded)]
        public void ActivitySourceAdapterPopulatesSamplingParamsCorrectlyForActivityWithRemoteParent(ActivityTraceFlags traceFlags)
        {
            var parentTraceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentTraceFlag = (traceFlags == ActivityTraceFlags.Recorded) ? "01" : "00";
            string remoteParentId = $"00-{parentTraceId}-{parentSpanId}-{parentTraceFlag}";
            string tracestate = "a=b;c=d";

            this.testSampler.SamplingAction = (samplingParameters) =>
            {
                Assert.Equal(parentTraceId, samplingParameters.ParentContext.TraceId);
                Assert.Equal(parentSpanId, samplingParameters.ParentContext.SpanId);
                Assert.Equal(traceFlags, samplingParameters.ParentContext.TraceFlags);
                Assert.Equal(tracestate, samplingParameters.ParentContext.TraceState);
                return new SamplingResult(true);
            };

            // Create an activity with remote parent id.
            // The sampling parameters are expected to be that of the
            // parent context i.e the remote parent.
            var activity = new Activity("test").SetParentId(remoteParentId);
            activity.TraceStateString = tracestate;
            activity.Start();
            this.activitySourceAdapter.Start(activity);
            activity.Stop();
            this.activitySourceAdapter.Stop(activity);
        }

        [Theory]
        [InlineData(ActivityTraceFlags.None)]
        [InlineData(ActivityTraceFlags.Recorded)]
        public void ActivitySourceAdapterPopulatesSamplingParamsCorrectlyForActivityWithInProcParent(ActivityTraceFlags traceFlags)
        {
            // Create some parent activity.
            string tracestate = "a=b;c=d";
            var activityLocalParent = new Activity("testParent");
            activityLocalParent.ActivityTraceFlags = traceFlags;
            activityLocalParent.TraceStateString = tracestate;
            activityLocalParent.Start();

            this.testSampler.SamplingAction = (samplingParameters) =>
            {
                Assert.Equal(activityLocalParent.TraceId, samplingParameters.ParentContext.TraceId);
                Assert.Equal(activityLocalParent.SpanId, samplingParameters.ParentContext.SpanId);
                Assert.Equal(activityLocalParent.ActivityTraceFlags, samplingParameters.ParentContext.TraceFlags);
                Assert.Equal(tracestate, samplingParameters.ParentContext.TraceState);
                return new SamplingResult(true);
            };

            // This activity will have a inproc parent.
            // activity.Parent will be equal to the activity created at the beginning of this test.
            // Sampling parameters are expected to be that of the parentContext.
            // i.e of the parent Activity
            var activity = new Activity("test");
            activity.Start();
            this.activitySourceAdapter.Start(activity);
            activity.Stop();
            this.activitySourceAdapter.Stop(activity);

            activityLocalParent.Stop();
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private class TestSampler : ActivitySampler
        {
            public Func<ActivitySamplingParameters, SamplingResult> SamplingAction { get; set; }

            public override string Description { get; } = nameof(TestSampler);

            public override SamplingResult ShouldSample(in ActivitySamplingParameters samplingParameters)
            {
                return this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(true);
            }
        }
    }
}
