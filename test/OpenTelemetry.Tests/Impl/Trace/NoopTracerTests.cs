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
            Assert.Same(BlankSpan.Instance, new ProxyTracer().CurrentSpan);
        }

        [Fact]
        public void NoopTracer_WithSpan()
        {
            var noopScope = new ProxyTracer().WithSpan(BlankSpan.Instance);
            Assert.NotNull(noopScope);
            // does not throw
            noopScope.Dispose();
        }

        [Fact]
        public void NoopTracer_CreateSpan_BadArgs()
        {
            var proxyTracer = new ProxyTracer();
            
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() =>
                proxyTracer.StartSpanFromActivity(null, new Activity("foo").Start()));

            Assert.Throws<ArgumentNullException>(() =>
                proxyTracer.StartSpanFromActivity("foo", null));

            Assert.Throws<ArgumentException>(() =>
                proxyTracer.StartSpanFromActivity("foo", new Activity("foo")));

            Assert.Throws<ArgumentException>(() => proxyTracer.StartSpanFromActivity(
                    "foo", 
                    new Activity("foo").SetIdFormat(ActivityIdFormat.Hierarchical).Start()));
        }

        [Fact]
        public void NoopTracer_CreateSpan_ValidArgs()
        {
            var proxyTracer = new ProxyTracer();
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo"));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo"));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default, null));

            var validActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Consumer));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Client, null));
        }

        [Fact]
        public void NoopTracer_Formats()
        {
            Assert.NotNull(new ProxyTracer().TextFormat);
            Assert.NotNull(new ProxyTracer().BinaryFormat);
            Assert.IsAssignableFrom<ITextFormat>(new ProxyTracer().TextFormat);
            Assert.IsAssignableFrom<IBinaryFormat>(new ProxyTracer().BinaryFormat);
        }
    }
}

