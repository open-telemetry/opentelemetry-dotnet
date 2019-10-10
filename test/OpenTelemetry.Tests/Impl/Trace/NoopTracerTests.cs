// <copyright file="NoopTracerTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Tests.Impl.Trace
{
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;
    using Xunit;

    public class NoopTracerTests
    {
        [Fact]
        public void NoopTracer_CurrentSpan()
        {
            Assert.Same(BlankSpan.Instance, NoopTracer.Instance.CurrentSpan);
        }

        [Fact]
        public void NoopTracer_WithSpan()
        {
            var noopScope = NoopTracer.Instance.WithSpan(BlankSpan.Instance);
            Assert.NotNull(noopScope);
            // does not throw
            noopScope.Dispose();
        }

        [Fact]
        public void NoopTracer_CreateSpan_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartRootSpan(null));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartRootSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartRootSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartRootSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, BlankSpan.Instance));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, BlankSpan.Instance, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanContext.Blank));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanContext.Blank, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanContext.Blank, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.StartSpan(null, SpanContext.Blank, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() =>
                NoopTracer.Instance.StartSpanFromActivity(null, new Activity("foo").Start()));

            Assert.Throws<ArgumentNullException>(() => 
                NoopTracer.Instance.StartSpanFromActivity("foo", null));

            Assert.Throws<ArgumentException>(() => 
                NoopTracer.Instance.StartSpanFromActivity("foo", new Activity("foo")));

            Assert.Throws<ArgumentException>(() => NoopTracer.Instance.StartSpanFromActivity(
                    "foo", 
                    new Activity("foo").SetIdFormat(ActivityIdFormat.Hierarchical).Start()));
        }

        [Fact]
        public void NoopTracer_CreateSpan_ValidArgs()
        {
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartRootSpan("foo"));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartRootSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartRootSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartRootSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo"));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", BlankSpan.Instance));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", BlankSpan.Instance, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanContext.Blank));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanContext.Blank, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default, null));

            var validActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpanFromActivity("foo", validActivity));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpanFromActivity("foo", validActivity, SpanKind.Consumer));
            Assert.Equal(BlankSpan.Instance, NoopTracer.Instance.StartSpanFromActivity("foo", validActivity, SpanKind.Client, null));
        }

        [Fact]
        public void NoopTracer_Formats()
        {
            Assert.NotNull(NoopTracer.Instance.TextFormat);
            Assert.NotNull(NoopTracer.Instance.BinaryFormat);
            Assert.IsAssignableFrom<ITextFormat>(NoopTracer.Instance.TextFormat);
            Assert.IsAssignableFrom<IBinaryFormat>(NoopTracer.Instance.BinaryFormat);
        }
    }
}

