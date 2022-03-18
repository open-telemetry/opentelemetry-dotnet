// <copyright file="ActivityEnumerationBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class ActivityEnumerationBenchmarks
    {
        private readonly ActivitySource source = new("Source");
        private readonly Activity activity;

        public ActivityEnumerationBenchmarks()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == "Source",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            });

            ActivityTagsCollection tags = new ActivityTagsCollection
            {
                { "tag1", "value1" },
                { "tag2", "value2" },
                { "tag3", "value3" },
                { "tag4", "value4" },
                { "tag5", "value5" },
            };

            this.activity = this.source.StartActivity(
                "test",
                ActivityKind.Internal,
                parentContext: default,
                links: new ActivityLink[]
                {
                    new ActivityLink(default, tags),
                    new ActivityLink(default, tags),
                    new ActivityLink(default, tags),
                    new ActivityLink(default, tags),
                    new ActivityLink(default, tags),
                });

            this.activity.SetTag("tag1", "value1");
            this.activity.SetTag("tag2", "value2");
            this.activity.SetTag("tag3", "value3");
            this.activity.SetTag("tag4", "value4");
            this.activity.SetTag("tag5", "value5");

            this.activity.AddEvent(new ActivityEvent("event1", tags: tags));
            this.activity.AddEvent(new ActivityEvent("event2", tags: tags));
            this.activity.AddEvent(new ActivityEvent("event3", tags: tags));
            this.activity.AddEvent(new ActivityEvent("event4", tags: tags));
            this.activity.AddEvent(new ActivityEvent("event5", tags: tags));

            this.activity.Stop();
        }

        [Benchmark]
        public void EnumerateTags()
        {
            TagEnumerationState state = default;

            this.activity.EnumerateTags(ref state);
        }

        [Benchmark]
        public void EnumerateLinks()
        {
            LinkEnumerationState state = default;

            this.activity.EnumerateLinks(ref state);
        }

        [Benchmark]
        public void EnumerateEvents()
        {
            EventEnumerationState state = default;

            this.activity.EnumerateEvents(ref state);
        }

        private struct TagEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public int Count;

            public bool ForEach(KeyValuePair<string, object> activityTag)
            {
                this.Count++;
                return true;
            }
        }

        private struct LinkEnumerationState : IActivityEnumerator<ActivityLink>
        {
            public int LinkCount;
            public int TagCount;

            public bool ForEach(ActivityLink activityLink)
            {
                this.LinkCount++;

                TagEnumerationState tags = default;
                activityLink.EnumerateTags(ref tags);
                this.TagCount += tags.Count;

                return true;
            }
        }

        private struct EventEnumerationState : IActivityEnumerator<ActivityEvent>
        {
            public int EventCount;
            public int TagCount;

            public bool ForEach(ActivityEvent activityEvent)
            {
                this.EventCount++;

                TagEnumerationState tags = default;
                activityEvent.EnumerateTags(ref tags);
                this.TagCount += tags.Count;

                return true;
            }
        }
    }
}
