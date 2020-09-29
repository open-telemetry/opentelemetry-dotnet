// <copyright file="ActivityExtensionsTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class ActivityExtensionsTest
    {
        private const string ActivitySourceName = "test.status";
        private const string ActivityName = "Test Activity";

        [Fact]
        public void SetStatus()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Ok);
            activity?.Stop();

            Assert.True(activity.GetStatus().IsOk);
        }

        [Fact]
        public void SetStatusWithDescription()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Error.WithDescription("Not Found"));
            activity?.Stop();

            var status = activity.GetStatus();
            Assert.Equal(StatusCode.Error, status.StatusCode);
            Assert.Equal("Not Found", status.Description);
        }

        [Fact]
        public void SetCancelledStatus()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Error);
            activity?.Stop();

            Assert.True(activity.GetStatus().StatusCode.Equals(Status.Error.StatusCode));
        }

        [Fact]
        public void GetStatusWithNoStatusInActivity()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity?.Stop();

            Assert.False(activity.GetStatus().IsValid);
        }

        [Fact]
        public void LastSetStatusWins()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(ActivityName);
            activity.SetStatus(Status.Error);
            activity.SetStatus(Status.Ok);
            activity?.Stop();

            Assert.True(activity.GetStatus().IsOk);
        }

        [Fact]
        public void CheckRecordException()
        {
            var message = "message";
            var exception = new ArgumentNullException(message, new Exception(message));
            var activity = new Activity("test-activity");
            activity.RecordException(exception);

            var @event = activity.Events.FirstOrDefault(e => e.Name == SemanticConventions.AttributeExceptionEventName);
            Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
            Assert.Equal(exception.GetType().Name, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        }

        [Fact]
        public void GetTagValueEmpty()
        {
            Activity activity = new Activity("Test");

            Assert.Null(activity.GetTagValue("Tag1"));
        }

        [Fact]
        public void GetTagValue()
        {
            Activity activity = new Activity("Test");
            activity.SetTag("Tag1", "Value1");

            Assert.Equal("Value1", activity.GetTagValue("Tag1"));
            Assert.Null(activity.GetTagValue("tag1"));
            Assert.Null(activity.GetTagValue("Tag2"));
        }

        [Fact]
        public void EnumerateTagValuesEmpty()
        {
            Activity activity = new Activity("Test");

            TagEnumerator state = default;

            activity.EnumerateTags(ref state);

            Assert.Equal(0, state.Count);
            Assert.False(state.LastTag.HasValue);
        }

        [Fact]
        public void EnumerateTagValuesAll()
        {
            Activity activity = new Activity("Test");
            activity.SetTag("Tag1", "Value1");
            activity.SetTag("Tag2", "Value2");
            activity.SetTag("Tag3", "Value3");

            TagEnumerator state = default;

            activity.EnumerateTags(ref state);

            Assert.Equal(3, state.Count);
            Assert.True(state.LastTag.HasValue);
            Assert.Equal("Tag3", state.LastTag?.Key);
            Assert.Equal("Value3", state.LastTag?.Value);
        }

        [Fact]
        public void EnumerateTagValuesWithBreak()
        {
            Activity activity = new Activity("Test");
            activity.SetTag("Tag1", "Value1");
            activity.SetTag("Tag2", "Value2");
            activity.SetTag("Tag3", "Value3");

            TagEnumerator state = default;
            state.BreakOnCount = 1;

            activity.EnumerateTags(ref state);

            Assert.Equal(1, state.Count);
            Assert.True(state.LastTag.HasValue);
            Assert.Equal("Tag1", state.LastTag?.Key);
            Assert.Equal("Value1", state.LastTag?.Value);
        }

        [Fact]
        public void EnumerateLinksEmpty()
        {
            Activity activity = new Activity("Test");

            LinkEnumerator state = default;

            activity.EnumerateLinks(ref state);

            Assert.Equal(0, state.Count);
            Assert.False(state.LastLink.HasValue);
        }

        [Fact]
        public void EnumerateLinksNonempty()
        {
            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .Build();

            var links = new[]
            {
                new ActivityLink(default),
                new ActivityLink(default),
                new ActivityLink(default),
            };

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity(
                ActivityName,
                ActivityKind.Internal,
                parentContext: default,
                links: links);

            LinkEnumerator state = default;

            activity.EnumerateLinks(ref state);

            Assert.Equal(3, state.Count);
            Assert.Equal(links.Last(), state.LastLink);
        }

        [Fact]
        public void EnumerateLinkTagsEmpty()
        {
            ActivityLink activityLink = new ActivityLink(default);

            TagEnumerator state = default;

            activityLink.EnumerateTags(ref state);

            Assert.Equal(0, state.Count);
            Assert.False(state.LastTag.HasValue);
        }

        [Fact]
        public void EnumerateLinkTagsNonempty()
        {
            ActivityLink activityLink = new ActivityLink(
                default,
                new ActivityTagsCollection(new Dictionary<string, object>
                {
                    ["tag1"] = "value1",
                    ["tag2"] = "value2",
                    ["tag3"] = "value3",
                }));

            TagEnumerator state = default;

            activityLink.EnumerateTags(ref state);

            Assert.True(state.LastTag.HasValue);
            Assert.Equal("tag3", state.LastTag?.Key);
            Assert.Equal("value3", state.LastTag?.Value);
        }

        [Fact]
        public void EnumerateEventsEmpty()
        {
            Activity activity = new Activity("Test");

            EventEnumerator state = default;

            activity.EnumerateEvents(ref state);

            Assert.Equal(0, state.Count);
            Assert.False(state.LastEvent.HasValue);
        }

        [Fact]
        public void EnumerateEventsNonempty()
        {
            Activity activity = new Activity("Test");

            activity.AddEvent(new ActivityEvent("event1"));
            activity.AddEvent(new ActivityEvent("event2"));
            activity.AddEvent(new ActivityEvent("event3"));

            EventEnumerator state = default;

            activity.EnumerateEvents(ref state);

            Assert.Equal(3, state.Count);
            Assert.Equal("event3", state.LastEvent.Value.Name);
        }

        [Fact]
        public void EnumerateEventTagsEmpty()
        {
            ActivityEvent activityEvent = new ActivityEvent(default);

            TagEnumerator state = default;

            activityEvent.EnumerateTags(ref state);

            Assert.Equal(0, state.Count);
            Assert.False(state.LastTag.HasValue);
        }

        [Fact]
        public void EnumerateEventTagsNonempty()
        {
            ActivityEvent activityEvent = new ActivityEvent(
                "event",
                tags: new ActivityTagsCollection(new Dictionary<string, object>
                {
                    ["tag1"] = "value1",
                    ["tag2"] = "value2",
                    ["tag3"] = "value3",
                }));

            TagEnumerator state = default;

            activityEvent.EnumerateTags(ref state);

            Assert.True(state.LastTag.HasValue);
            Assert.Equal("tag3", state.LastTag?.Key);
            Assert.Equal("value3", state.LastTag?.Value);
        }

        private struct TagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public int BreakOnCount { get; set; }

            public KeyValuePair<string, object>? LastTag { get; private set; }

            public int Count { get; private set; }

            public bool ForEach(KeyValuePair<string, object> item)
            {
                this.LastTag = item;
                this.Count++;
                if (this.BreakOnCount == this.Count)
                {
                    return false;
                }

                return true;
            }
        }

        private struct LinkEnumerator : IActivityEnumerator<ActivityLink>
        {
            public ActivityLink? LastLink { get; private set; }

            public int Count { get; private set; }

            public bool ForEach(ActivityLink item)
            {
                this.LastLink = item;

                this.Count++;

                return true;
            }
        }

        private struct EventEnumerator : IActivityEnumerator<ActivityEvent>
        {
            public ActivityEvent? LastEvent { get; private set; }

            public int Count { get; private set; }

            public bool ForEach(ActivityEvent item)
            {
                this.LastEvent = item;

                this.Count++;

                return true;
            }
        }
    }
}
