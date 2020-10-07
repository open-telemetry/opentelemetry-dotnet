// <copyright file="ActivityBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Benchmarks
{
    [MemoryDiagnoser]
    public class ActivityBenchmarks
    {
        private static readonly Activity EmptyActivity = new Activity("EmptyActivity");
        private static readonly Activity Activity;

        static ActivityBenchmarks()
        {
            using ActivitySource activitySource = new ActivitySource("Benchmarks");

            ActivitySource.AddActivityListener(
                new ActivityListener
                {
                    ShouldListenTo = (source) => true,
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                });

            var activityLinks = new[]
            {
                new ActivityLink(default),
                new ActivityLink(default),
                new ActivityLink(default),
            };

            Activity = activitySource.StartActivity(
                "Activity",
                ActivityKind.Internal,
                parentContext: default,
                links: activityLinks);

            Activity.SetTag("Tag1", "Value1");
            Activity.SetTag("Tag2", 2);
            Activity.SetTag("Tag3", false);

            for (int i = 0; i < 1024; i++)
            {
                Activity.AddTag($"AutoTag{i}", i);
            }

            Activity.AddEvent(new ActivityEvent("event1"));
            Activity.AddEvent(new ActivityEvent("event2"));
            Activity.AddEvent(new ActivityEvent("event3"));

            Activity.Stop();
        }

        [Benchmark]
        public void SearchEnumerateEmptyTagObjects()
        {
            object value;
            foreach (KeyValuePair<string, object> tag in EmptyActivity.TagObjects)
            {
                if (tag.Key == "Tag3")
                {
                    value = tag.Value;
                    break;
                }
            }
        }

        [Benchmark]
        public void SearchLinqEmptyTagObjects()
        {
            EmptyActivity.TagObjects.FirstOrDefault(i => i.Key == "Tag3");
        }

        [Benchmark]
        public void SearchGetTagValueEmptyTagObjects()
        {
            EmptyActivity.GetTagValue("Tag3");
        }

        [Benchmark]
        public void SearchEnumerateNonemptyTagObjects()
        {
            object value;
            foreach (KeyValuePair<string, object> tag in Activity.TagObjects)
            {
                if (tag.Key == "Tag3")
                {
                    value = tag.Value;
                    break;
                }
            }
        }

        [Benchmark]
        public void SearchLinqNonemptyTagObjects()
        {
            Activity.TagObjects.FirstOrDefault(i => i.Key == "Tag3");
        }

        [Benchmark]
        public void SearchGetTagValueNonemptyTagObjects()
        {
            Activity.GetTagValue("Tag3");
        }

        [Benchmark]
        public void EnumerateNonemptyTagObjects()
        {
            int count = 0;
            foreach (KeyValuePair<string, object> tag in Activity.TagObjects)
            {
                count++;
            }
        }

        [Benchmark]
        public void EnumerateTagValuesNonemptyTagObjects()
        {
            TagEnumerator state = default;

            Activity.EnumerateTagValues(ref state);
        }

        [Benchmark]
        public void EnumerateNonemptyActivityLinks()
        {
            int count = 0;
            foreach (ActivityLink activityLink in Activity.Links)
            {
                count++;
            }
        }

        [Benchmark]
        public void EnumerateLinksNonemptyActivityLinks()
        {
            LinkEnumerator state = default;

            Activity.EnumerateLinks(ref state);
        }

        [Benchmark]
        public void EnumerateNonemptyActivityEvents()
        {
            int count = 0;
            foreach (ActivityEvent activityEvent in Activity.Events)
            {
                count++;
            }
        }

        [Benchmark]
        public void EnumerateEventsNonemptyActivityEvents()
        {
            EventEnumerator state = default;

            Activity.EnumerateEvents(ref state);
        }

        private struct TagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public int Count { get; private set; }

            public bool ForEach(KeyValuePair<string, object> item)
            {
                this.Count++;
                return true;
            }
        }

        private struct LinkEnumerator : IActivityEnumerator<ActivityLink>
        {
            public int Count { get; private set; }

            public bool ForEach(ActivityLink item)
            {
                this.Count++;
                return true;
            }
        }

        private struct EventEnumerator : IActivityEnumerator<ActivityEvent>
        {
            public int Count { get; private set; }

            public bool ForEach(ActivityEvent item)
            {
                this.Count++;
                return true;
            }
        }
    }
}
