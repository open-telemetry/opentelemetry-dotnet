// <copyright file="BroadcastProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Export.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class BroadcastProcessorTests
    {
        [Fact]
        public void BroadcastProcessor_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new BroadcastProcessor(null));
            Assert.Throws<ArgumentException>(() => new BroadcastProcessor(new SimpleSpanProcessor[0]));
        }

        [Fact]
        public void BroadcastProcessor_CallsAllProcessorSequentially()
        {
            bool start1Called = false;
            bool start2Called = false;
            bool end1Called = false;
            bool end2Called = false;
            var processor1 = new TestProcessor(ss =>
            {
                start1Called = true;
                Assert.False(start2Called);
                Assert.False(end1Called);
                Assert.False(end2Called);
            }, se =>
            {
                end1Called = true;
                Assert.True(start1Called);
                Assert.True(start2Called);
                Assert.False(end2Called);
            });
            var processor2 = new TestProcessor(ss =>
            {
                start2Called = true;
                Assert.True(start1Called);
                Assert.False(end1Called);
                Assert.False(end2Called);
            }, se =>
            {
                end2Called = true;
                Assert.True(start1Called);
                Assert.True(start2Called);
                Assert.True(end1Called);

            });

            var broadcastProcessor = new BroadcastProcessor(new[] { processor1, processor2 });

            var tracer = TracerFactory.Create(_ => { }).GetTracer(null);
            var span = (SpanSdk)tracer.StartSpan("foo");

            var spanData = new SpanData(span);
            broadcastProcessor.OnStart(spanData);
            Assert.True(start1Called);
            Assert.True(start2Called);

            broadcastProcessor.OnEnd(spanData);
            Assert.True(end1Called);
            Assert.True(end2Called);
        }

        [Fact]
        public void BroadcastProcessor_OneProcessorThrows()
        {
            bool start1Called = false;
            bool start2Called = false;
            bool end1Called = false;
            bool end2Called = false;
            var processor1 = new TestProcessor(ss =>
            {
                start1Called = true;
                Assert.False(start2Called);
                Assert.False(end1Called);
                Assert.False(end2Called);

                throw new Exception("Start exception");
            }, se =>
            {
                end1Called = true;
                Assert.True(start1Called);
                Assert.True(start2Called);
                Assert.False(end2Called);
                throw new Exception("End exception");
            });

            var processor2 = new TestProcessor(ss =>
            {
                start2Called = true;
                Assert.True(start1Called);
                Assert.False(end1Called);
                Assert.False(end2Called);
            }, se =>
            {
                end2Called = true;
                Assert.True(start1Called);
                Assert.True(start2Called);
                Assert.True(end1Called);

            });

            var broadcastProcessor = new BroadcastProcessor(new[] { processor1, processor2 });

            var tracer = TracerFactory.Create(_ => { }).GetTracer(null);
            var span = (SpanSdk)tracer.StartSpan("foo");

            var spanData = new SpanData(span);
            broadcastProcessor.OnStart(spanData);
            Assert.True(start1Called);
            Assert.True(start2Called);

            broadcastProcessor.OnEnd(spanData);
            Assert.True(end1Called);
            Assert.True(end2Called);
        }

        [Fact]
        public void BroadcastProcessor_ShutsDownAll()
        {
            var processor1 = new TestProcessor(null, null);
            var processor2 = new TestProcessor(null, null);

            var broadcastProcessor = new BroadcastProcessor(new[] { processor1, processor2 });

            broadcastProcessor.ShutdownAsync(default);
            Assert.True(processor1.ShutdownCalled);
            Assert.True(processor2.ShutdownCalled);

            broadcastProcessor.Dispose();
            Assert.True(processor1.DisposedCalled);
            Assert.True(processor2.DisposedCalled);
        }

        private class TestProcessor : SpanProcessor, IDisposable
        {
            private readonly Action<SpanData> onStart;
            private readonly Action<SpanData> onEnd;
            public bool ShutdownCalled { get; private set; } = false;
            public bool DisposedCalled { get; private set; } = false;

            public TestProcessor(Action<SpanData> onStart, Action<SpanData> onEnd)
            {
                this.onStart = onStart;
                this.onEnd = onEnd;
            }

            public override void OnStart(SpanData span)
            {
                this.onStart?.Invoke(span);
            }

            public override void OnEnd(SpanData span)
            {
                this.onEnd?.Invoke(span);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                this.ShutdownCalled = true;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                this.DisposedCalled = true;
            }
        }
    }
}
