// <copyright file="SpanShimTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class SpanShimTests
    {
        private const string SpanName = "MySpanName/1";
        private const string TracerName = "defaultactivitysource";

        static SpanShimTests()
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
            Assert.Throws<ArgumentNullException>(() => new SpanShim(null));
        }

        [Fact]
        public void SpanContextIsNotNull()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            // ISpanContext validation handled in a separate test class
            Assert.NotNull(shim.Context);
        }

        [Fact]
        public void FinishSpan()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            shim.Finish();

            Assert.NotEqual(default, shim.Span.Activity.Duration);
        }

        [Fact]
        public void FinishSpanUsingSpecificTimestamp()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            var endTime = DateTimeOffset.UtcNow;
            shim.Finish(endTime);

            Assert.Equal(endTime - shim.Span.Activity.StartTimeUtc, shim.Span.Activity.Duration);
        }

        [Fact]
        public void SetOperationName()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.SetOperationName(null));

            shim.SetOperationName("bar");
            Assert.Equal("bar", shim.Span.Activity.DisplayName);
        }

        [Fact]
        public void GetBaggageItem()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.GetBaggageItem(null));

            // TODO - Method not implemented
        }

        [Fact]
        public void Log()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            shim.Log("foo");

            Assert.Single(shim.Span.Activity.Events);
            var first = shim.Span.Activity.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogWithExplicitTimestamp()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            var now = DateTimeOffset.UtcNow;
            shim.Log(now, "foo");

            Assert.Single(shim.Span.Activity.Events);
            var first = shim.Span.Activity.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.Equal(now, first.Timestamp);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogUsingFields()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

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

            var first = shim.Span.Activity.Events.FirstOrDefault();
            var last = shim.Span.Activity.Events.LastOrDefault();

            Assert.Equal(2, shim.Span.Activity.Events.Count());

            Assert.Equal(SpanShim.DefaultEventName, first.Name);
            Assert.True(first.Attributes.Any());

            Assert.Equal("foo", last.Name);
            Assert.False(last.Attributes.Any());
        }

        [Fact]
        public void LogUsingFieldsWithExplicitTimestamp()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

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

            Assert.Equal(2, shim.Span.Activity.Events.Count());
            var first = shim.Span.Activity.Events.First();
            var last = shim.Span.Activity.Events.Last();

            Assert.Equal(SpanShim.DefaultEventName, first.Name);
            Assert.True(first.Attributes.Any());
            Assert.Equal(now, first.Timestamp);

            Assert.Equal("foo", last.Name);
            Assert.False(last.Attributes.Any());
            Assert.Equal(now, last.Timestamp);
        }

        [Fact]
        public void SetTagStringValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, "foo"));

            shim.SetTag("foo", "bar");

            Assert.Single(shim.Span.Activity.Tags);
            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal("bar", shim.Span.Activity.Tags.First().Value);
        }

        [Fact]
        public void SetTagBoolValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, true));

            shim.SetTag("foo", true);
            shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.True(bool.Parse(shim.Span.Activity.Tags.First().Value));

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, shim.Span.Activity.GetStatus());

            // TODO: Activity object does not allow Tags update. Below lines of code needs to be enabled after .NET introducing SetTag on Activity.
            // shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            // Assert.Equal(Status.Ok, shim.Span.Activity.GetStatus());
        }

        [Fact]
        public void SetTagIntValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, 1));

            shim.SetTag("foo", 1);

            Assert.Single(shim.Span.Activity.Tags);
            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.Span.Activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagDoubleValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag(null, 1D));

            shim.SetTag("foo", 1D);

            Assert.Single(shim.Span.Activity.Tags);
            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal(1, double.Parse(shim.Span.Activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagBooleanTagValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((BooleanTag)null, true));

            shim.SetTag(new BooleanTag("foo"), true);
            shim.SetTag(new BooleanTag(global::OpenTracing.Tag.Tags.Error.Key), true);

            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.True(bool.Parse(shim.Span.Activity.Tags.First().Value));

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, shim.Span.Activity.GetStatus());

            // TODO: .NET does not allow Tags update. Below lines of code needs to be enabled after .NET introducing SetTag on Activity.
            // shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            // Assert.Equal(Status.Ok, shim.Span.Activity.GetStatus());
        }

        [Fact]
        public void SetTagStringTagValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((StringTag)null, "foo"));

            shim.SetTag(new StringTag("foo"), "bar");

            Assert.Single(shim.Span.Activity.Tags);
            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal("bar", shim.Span.Activity.Tags.First().Value);
        }

        [Fact]
        public void SetTagIntTagValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntTag)null, 1));

            shim.SetTag(new IntTag("foo"), 1);

            Assert.Single(shim.Span.Activity.Tags);
            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.Span.Activity.Tags.First().Value));
        }

        [Fact]
        public void SetTagIntOrStringTagValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanShim(tracer.StartSpan(SpanName));

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntOrStringTag)null, "foo"));

            shim.SetTag(new IntOrStringTag("foo"), 1);
            shim.SetTag(new IntOrStringTag("bar"), "baz");

            Assert.Equal(2, shim.Span.Activity.Tags.Count());

            Assert.Equal("foo", shim.Span.Activity.Tags.First().Key);
            Assert.Equal(1L, int.Parse(shim.Span.Activity.Tags.First().Value));

            Assert.Equal("bar", shim.Span.Activity.Tags.Last().Key);
            Assert.Equal("baz", shim.Span.Activity.Tags.Last().Value);
        }
    }
}
