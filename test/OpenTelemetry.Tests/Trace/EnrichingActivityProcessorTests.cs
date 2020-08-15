// <copyright file="EnrichingActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public sealed class EnrichingActivityProcessorTests : IDisposable
    {
        private static readonly Action<Activity> EnrichmentAction = (a) => a.AddTag("enriched", true);
        private readonly ActivitySource activitySource = new ActivitySource(nameof(EnrichingActivityProcessorTests));
        private readonly TestActivityProcessor testActivityProcessor = new TestActivityProcessor(collectEndedSpans: true);
        private readonly IDisposable sdk;

        public EnrichingActivityProcessorTests()
        {
            this.sdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(this.activitySource.Name)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessor(new EnrichingActivityProcessor())
                .AddProcessor(this.testActivityProcessor)
                .Build();
        }

        public void Dispose()
        {
            this.sdk.Dispose();
            this.activitySource.Dispose();
        }

        [Fact]
        public void SpanNotEnrichedTest()
        {
            using var reset = this.testActivityProcessor.ResetWhenDone();

            var activity = this.activitySource.StartActivity("Test", ActivityKind.Internal);
            activity.Stop();

            Assert.Null(EnrichmentScope.Current);
            Assert.Equal(1, this.testActivityProcessor.EndedActivityObjects.Count);
            Assert.Empty(this.testActivityProcessor.EndedActivityObjects[0].TagObjects);
        }

        [Fact]
        public void NextSpanEnrichedTest()
        {
            using var reset = this.testActivityProcessor.ResetWhenDone();

            Activity activity;

            using (EnrichmentScope.Begin(EnrichmentAction, EnrichmentScopeTarget.NextActivity))
            {
                activity = this.activitySource.StartActivity("Test1", ActivityKind.Internal);
                activity.Stop();

                activity = this.activitySource.StartActivity("Test2", ActivityKind.Internal);
                activity.Stop();
            }

            activity = this.activitySource.StartActivity("Test3", ActivityKind.Internal);
            activity.Stop();

            Assert.Null(EnrichmentScope.Current);
            Assert.Equal(3, this.testActivityProcessor.EndedActivityObjects.Count);
            Assert.Contains(this.testActivityProcessor.EndedActivityObjects[0].TagObjects, i => i.Key == "enriched" && (i.Value as bool?) == true);
            Assert.Empty(this.testActivityProcessor.EndedActivityObjects[1].TagObjects);
            Assert.Empty(this.testActivityProcessor.EndedActivityObjects[2].TagObjects);
        }

        [Fact]
        public void ChildSpansEnrichedTest()
        {
            using var reset = this.testActivityProcessor.ResetWhenDone();

            Activity activity;

            using (EnrichmentScope.Begin(EnrichmentAction, EnrichmentScopeTarget.AllChildren))
            {
                activity = this.activitySource.StartActivity("Test1", ActivityKind.Internal);
                activity.Stop();

                activity = this.activitySource.StartActivity("Test2", ActivityKind.Internal);
                activity.Stop();
            }

            activity = this.activitySource.StartActivity("Test3", ActivityKind.Internal);
            activity.Stop();

            Assert.Null(EnrichmentScope.Current);
            Assert.Equal(3, this.testActivityProcessor.EndedActivityObjects.Count);
            Assert.Contains(this.testActivityProcessor.EndedActivityObjects[0].TagObjects, i => i.Key == "enriched" && (i.Value as bool?) == true);
            Assert.Contains(this.testActivityProcessor.EndedActivityObjects[1].TagObjects, i => i.Key == "enriched" && (i.Value as bool?) == true);
            Assert.Empty(this.testActivityProcessor.EndedActivityObjects[2].TagObjects);
        }

        [Fact]
        public void MixedTargetEnrichedTest()
        {
            using var reset = this.testActivityProcessor.ResetWhenDone();

            Activity activity;

            using (EnrichmentScope.Begin((a) => a.AddTag("enrichment.top", true), EnrichmentScopeTarget.AllChildren))
            {
                activity = this.activitySource.StartActivity("Test1", ActivityKind.Internal); // Test1 <- enrichment.top
                activity.Stop();

                using (EnrichmentScope.Begin((a) => a.AddTag("enrichment.next", 1), EnrichmentScopeTarget.NextActivity))
                {
                    activity = this.activitySource.StartActivity("Test2", ActivityKind.Internal); // Test2 <- enrichment.top, enrichment.next=1
                    activity.Stop();
                }

                using (EnrichmentScope.Begin((a) => a.AddTag("enrichment.next", 2), EnrichmentScopeTarget.NextActivity))
                {
                    using (EnrichmentScope.Begin((a) => a.AddTag("enrichment.inner", true), EnrichmentScopeTarget.AllChildren))
                    {
                        activity = this.activitySource.StartActivity("Test3", ActivityKind.Internal); // Test3 <- enrichment.top, enrichment.next=2, enrichment.inner
                        activity.Stop();

                        activity = this.activitySource.StartActivity("Test4", ActivityKind.Internal); // Test4 <- enrichment.top, enrichment.inner
                        activity.Stop();
                    }
                }

                activity = this.activitySource.StartActivity("Test5", ActivityKind.Internal); // Test5 <- enrichment.top
                activity.Stop();
            }

            activity = this.activitySource.StartActivity("Test6", ActivityKind.Internal);
            activity.Stop();

            Assert.Null(EnrichmentScope.Current);

            Assert.Equal(6, this.testActivityProcessor.EndedActivityObjects.Count);

            var activities = this.testActivityProcessor.EndedActivityObjects;

            EnsureActivityMatchesTags(activities[0], new KeyValuePair<string, object>("enrichment.top", true));
            EnsureActivityMatchesTags(activities[1], new KeyValuePair<string, object>("enrichment.top", true), new KeyValuePair<string, object>("enrichment.next", 1));
            EnsureActivityMatchesTags(activities[2], new KeyValuePair<string, object>("enrichment.top", true), new KeyValuePair<string, object>("enrichment.next", 2), new KeyValuePair<string, object>("enrichment.inner", true));
            EnsureActivityMatchesTags(activities[3], new KeyValuePair<string, object>("enrichment.top", true), new KeyValuePair<string, object>("enrichment.inner", true));
            EnsureActivityMatchesTags(activities[4], new KeyValuePair<string, object>("enrichment.top", true));
            Assert.Empty(activities[5].TagObjects);
        }

        private static void EnsureActivityMatchesTags(Activity activity, params KeyValuePair<string, object>[] tags)
        {
            var tagObjects = activity.TagObjects;

            Assert.Equal(tags.Length, tagObjects.Count());

            foreach (KeyValuePair<string, object> tag in tags)
            {
                Assert.Contains(tagObjects, i => i.Key == tag.Key && i.Value.ToString() == tag.Value.ToString());
            }
        }
    }
}
