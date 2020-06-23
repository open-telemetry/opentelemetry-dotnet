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
using OpenTelemetry.Tests.Implementation.Testing.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace
{
    public class ActivitySourceAdapterTest
    {
        private TestSampler testSampler;
        private TestActivityProcessor testProcessor;

        static ActivitySourceAdapterTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        public ActivitySourceAdapterTest()
        {
            this.testSampler = new TestSampler();
        }

        [Theory]
        [InlineData("")]
        [InlineData("00-e4d9d231de64d6408047ad4757e95422-05d365cd170e544d-00")]
        public void ActivitySourceAdapterSamplesCorrectly(string activityParentId)
        {
            this.testSampler.SamplingAction = (samplingParameters) =>
            {
                Assert.Equal(default, samplingParameters.ParentContext);
                return new SamplingResult(true);
            }

            bool startCalled = false;
            bool endCalled = false;
            TestActivityProcessor testProcessor = new TestActivityProcessor(
                ss =>
                {
                    startCalled = true;
                    Assert.True(startCalled);
                }, se =>
                {
                    endCalled = true;
                    Assert.False(endCalled);
                });

            ActivitySourceAdapter activitySourceAdapter = new ActivitySourceAdapter(testSampler, testProcessor);
            var activity = new Activity("test");
            if (!string.IsNullOrEmpty(activityParentId))
            {
                activity.SetParentId(activityParentId);
            }

            activity.Start();
            activitySourceAdapter.Start(activity);
            activity.Stop();
            activitySourceAdapter.Stop(activity);
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
