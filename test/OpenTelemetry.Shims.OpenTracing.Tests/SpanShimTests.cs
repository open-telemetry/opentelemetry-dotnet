// <copyright file="SpanShimTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Linq;
using global::OpenTracing.Tag;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class SpanShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanShim(null));
        }

        [Fact]
        public void SpanContextIsNotNull()
        {
            var shim = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);

            // ISpanContext validation handled in a separate test class
            Assert.NotNull(shim.Context);
        }

        [Fact]
        public void FinishSpan()
        {
            var spanMock = Defaults.GetOpenTelemetryMockSpan();
            var shim = new SpanShim(spanMock.Object);

            shim.Finish();

            spanMock.Verify(o => o.End(), Times.Once());
        }

        [Fact]
        public void FinishSpanUsingSpecificTimestamp()
        {
            var spanMock = Defaults.GetOpenTelemetryMockSpan();
            var shim = new SpanShim(spanMock.Object);

            var endTime = DateTimeOffset.UtcNow;
            shim.Finish(endTime);

            spanMock.Verify(o => o.End(endTime), Times.Once());
        }

        [Fact]
        public void SetOperationName()
        {
            var spanMock = Defaults.GetOpenTelemetryMockSpan();
            var shim = new SpanShim(spanMock.Object);

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.SetOperationName(null));

            shim.SetOperationName("bar");

            spanMock.Verify(o => o.UpdateName("bar"), Times.Once());
        }

        [Fact]
        public void GetBaggageItem()
        {
            var spanMock = Defaults.GetOpenTelemetryMockSpan();
            var shim = new SpanShim(spanMock.Object);

            // parameter validation
            Assert.Throws<ArgumentNullException>(() => shim.GetBaggageItem(null));

            // TODO
        }

        [Fact]
        public void Log()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            shim.Log("foo");

            Assert.Single(spanMock.Events);
            var first = spanMock.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogWithExplicitTimestamp()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            var now = DateTimeOffset.UtcNow;
            shim.Log(now, "foo");

            Assert.Single(spanMock.Events);
            var first = spanMock.Events.First();
            Assert.Equal("foo", first.Name);
            Assert.Equal(now, first.Timestamp);
            Assert.False(first.Attributes.Any());
        }

        [Fact]
        public void LogUsingFields()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

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

            var first = spanMock.Events.FirstOrDefault();
            var last = spanMock.Events.LastOrDefault();

            Assert.Equal(2, spanMock.Events.Count);

            Assert.Equal(SpanShim.DefaultEventName, first.Name);
            Assert.True(first.Attributes.Any());

            Assert.Equal("foo", last.Name);
            Assert.False(last.Attributes.Any());
        }

        [Fact]
        public void LogUsingFieldsWithExplicitTimestamp()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

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

            Assert.Equal(2, spanMock.Events.Count);
            var first = spanMock.Events.First();
            var last = spanMock.Events.Last();

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
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, "foo"));

            shim.SetTag("foo", "bar");

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal("bar", spanMock.Attributes.First().Value);
        }

        [Fact]
        public void SetTagBoolValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, true));

            shim.SetTag("foo", true);
            shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.True((bool)spanMock.Attributes.First().Value);

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, spanMock.Status);
            shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            Assert.Equal(Status.Ok, spanMock.Status);
        }

        [Fact]
        public void SetTagIntValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null, 1));

            shim.SetTag("foo", 1);

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal(1L, spanMock.Attributes.First().Value);
        }

        [Fact]
        public void SetTagDoubleValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag(null, 1D));

            shim.SetTag("foo", 1D);

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal(1, (double)spanMock.Attributes.First().Value);
        }

        [Fact]
        public void SetTagBooleanTagValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((BooleanTag)null, true));

            shim.SetTag(new BooleanTag("foo"), true);
            shim.SetTag(new BooleanTag(global::OpenTracing.Tag.Tags.Error.Key), true);

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.True((bool)spanMock.Attributes.First().Value);

            // A boolean tag named "error" is a special case that must be checked
            Assert.Equal(Status.Unknown, spanMock.Status);
            shim.SetTag(global::OpenTracing.Tag.Tags.Error.Key, false);
            Assert.Equal(Status.Ok, spanMock.Status);
        }

        [Fact]
        public void SetTagStringTagValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((StringTag)null, "foo"));

            shim.SetTag(new StringTag("foo"), "bar");

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal("bar", (string)spanMock.Attributes.First().Value);
        }

        [Fact]
        public void SetTagIntTagValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntTag)null, 1));

            shim.SetTag(new IntTag("foo"), 1);

            Assert.Single(spanMock.Attributes);
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal(1L, spanMock.Attributes.First().Value);
        }

        [Fact]
        public void SetTagIntOrStringTagValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanShim(spanMock);

            Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntOrStringTag)null, "foo"));

            shim.SetTag(new IntOrStringTag("foo"), 1);
            shim.SetTag(new IntOrStringTag("bar"), "baz");

            Assert.Equal(2, spanMock.Attributes.Count);

            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal(1L, spanMock.Attributes.First().Value);

            Assert.Equal("bar", spanMock.Attributes.Last().Key);
            Assert.Equal("baz", (string)spanMock.Attributes.Last().Value);
        }
    }
}
