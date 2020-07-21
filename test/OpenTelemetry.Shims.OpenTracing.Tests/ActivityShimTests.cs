// <copyright file="ActivityShimTests.cs" company="OpenTelemetry Authors">
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
using global::OpenTracing.Tag;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class ActivityShimTests
    {
        private const string ActivityName1 = "MyActivityName/1";
        private const string ActivitySourceName = "defaultactivitysource";

        static ActivityShimTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new ActivityShim(null));
        }

        [Fact]
        public void SpanContextIsNotNull()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            // ISpanContext validation handled in a separate test class
            Assert.NotNull(shim.Context);
        }

        [Fact]
        public void FinishSpan()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            shim.Finish();

            Assert.NotEqual(default, shim.activity.Duration);
        }

        [Fact]
        public void FinishSpanUsingSpecificTimestamp()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            var endTime = DateTimeOffset.UtcNow;
            shim.Finish(endTime);

            Assert.Equal(endTime - shim.activity.StartTimeUtc, shim.activity.Duration);
        }

        [Fact]
        public void SetOperationName()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.SetOperationName(null));

            shim.SetOperationName("bar");
            Assert.Equal("bar", shim.activity.DisplayName);
        }

        [Fact]
        public void GetBaggageItem()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.GetBaggageItem(null));

            shim.SetBaggageItem("TestBaggageKey", "TestBaggageValue");
            Assert.Equal("TestBaggageValue", shim.GetBaggageItem("TestBaggageKey"));
        }

        [Fact]
        public void Log()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            shim.Log("foo");

            Assert.Single(shim.activity.Events);
            var first = shim.activity.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogWithExplicitTimestamp()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            var now = DateTimeOffset.UtcNow;
            shim.Log(now, "foo");

            Assert.Single(shim.activity.Events);
            var first = shim.activity.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.Equal(now, first.Timestamp);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogUsingFields()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.Log((IEnumerable<KeyValuePair<string, object>>)null));

            shim.Log(new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("foo", "bar"),
            });

            // "event" is a special event name
            shim.Log(new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("event", "foo"),
            });

            var first = shim.activity.Events.FirstOrDefault();
            var last = shim.activity.Events.LastOrDefault();

            Assert.Equal(2, shim.activity.Events.Count());

            Assert.Equal(ActivityShim.DefaultEventName, first.Name);
            Assert.True(first.Attributes.Any());

            Assert.Equal("foo", last.Name);
            Assert.False(last.Attributes.Any());
        }

        [Fact]
        public void LogUsingFieldsWithExplicitTimestamp()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.Log((IEnumerable<KeyValuePair<string, object>>)null));
            var now = DateTimeOffset.UtcNow;

            shim.Log(now, new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("foo", "bar"),
            });

            // "event" is a special event name
            shim.Log(now, new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("event", "foo"),
            });

            Assert.Equal(2, shim.activity.Events.Count());
            var first = shim.activity.Events.First();
            var last = shim.activity.Events.Last();

            Assert.Equal(ActivityShim.DefaultEventName, first.Name);
            Assert.True(first.Attributes.Any());
            Assert.Equal(now, first.Timestamp);

            Assert.Equal("foo", last.Name);
            Assert.False(last.Attributes.Any());
            Assert.Equal(now, last.Timestamp);
        }

        [Fact]
        public void SetTagStringValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, "foo"));

            shim.SetTag("foo", "bar");

            Assert.Single(shim.activity.Tags);
            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal("bar", shim.activity.Tags.First().Value);
        }

        [Fact]
        public void SetTagBoolValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, true));

            shim.SetTag("foo", true);
            shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.True(bool.Parse(shim.activity.Tags.First().Value));

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, shim.activity.GetStatus());

            // TODO: Activity object does not allow Tags update. Below lines of code needs to be enabled after .NET introducing SetTag on Activity.
            // shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            // Assert.Equal(Status.Ok, shim.activity.GetStatus());
        }

        [Fact]
        public void SetTagIntValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, 1));

            shim.SetTag("foo", 1);

            Assert.Single(shim.activity.Tags);
            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagDoubleValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag(null, 1D));

            shim.SetTag("foo", 1D);

            Assert.Single(shim.activity.Tags);
            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal(1, double.Parse(shim.activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagBooleanTagValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((BooleanTag)null, true));

            shim.SetTag(new BooleanTag("foo"), true);
            shim.SetTag(new BooleanTag(global::OpenTracing.Tag.Tags.Error.Key), true);

            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.True(bool.Parse(shim.activity.Tags.First().Value));

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, shim.activity.GetStatus());

            // TODO: .NET does not allow Tags update. Below lines of code needs to be enabled after .NET introducing SetTag on Activity.
            // shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            // Assert.Equal(Status.Ok, shim.activity.GetStatus());
        }

        [Fact]
        public void SetTagStringTagValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((StringTag)null, "foo"));

            shim.SetTag(new StringTag("foo"), "bar");

            Assert.Single(shim.activity.Tags);
            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal("bar", shim.activity.Tags.First().Value);
        }

        [Fact]
        public void SetTagIntTagValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntTag)null, 1));

            shim.SetTag(new IntTag("foo"), 1);

            Assert.Single(shim.activity.Tags);
            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagIntOrStringTagValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityShim(activitySource.StartActivity(ActivityName1));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntOrStringTag)null, "foo"));

            shim.SetTag(new IntOrStringTag("foo"), 1);
            shim.SetTag(new IntOrStringTag("bar"), "baz");

            Assert.Equal(2, shim.activity.Tags.Count());

            Assert.Equal("foo", shim.activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.activity.Tags.First().Value));

            Assert.Equal("bar", shim.activity.Tags.Last().Key);
            Assert.Equal("baz", shim.activity.Tags.Last().Value);
        }
    }
}
