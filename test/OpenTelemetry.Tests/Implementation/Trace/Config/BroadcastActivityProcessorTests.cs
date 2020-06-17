// <copyright file="BroadcastActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Export.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class BroadcastActivityProcessorTests
    {
        [Fact]
        public void BroadcastProcessor_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new BroadcastActivityProcessor(null));
            Assert.Throws<ArgumentException>(() => new BroadcastActivityProcessor(new SimpleActivityProcessor[0]));
        }

        [Fact]
        public void BroadcastProcessor_CallsAllProcessorSequentially()
        {
            bool start1Called = false;
            bool start2Called = false;
            bool end1Called = false;
            bool end2Called = false;
            var processor1 = new TestProcessor(
                ss =>
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
            var processor2 = new TestProcessor(
                ss =>
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

            var broadcastProcessor = new BroadcastActivityProcessor(new[] { processor1, processor2 });

            var activity = new Activity("somename");
            broadcastProcessor.OnStart(activity);
            Assert.True(start1Called);
            Assert.True(start2Called);

            broadcastProcessor.OnEnd(activity);
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
            var processor1 = new TestProcessor(
                ss =>
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

            var processor2 = new TestProcessor(
                ss =>
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

            var broadcastProcessor = new BroadcastActivityProcessor(new[] { processor1, processor2 });

            var activity = new Activity("somename");
            broadcastProcessor.OnStart(activity);
            Assert.True(start1Called);
            Assert.True(start2Called);

            broadcastProcessor.OnEnd(activity);
            Assert.True(end1Called);
            Assert.True(end2Called);
        }

        [Fact]
        public void BroadcastProcessor_ShutsDownAll()
        {
            var processor1 = new TestProcessor(null, null);
            var processor2 = new TestProcessor(null, null);

            var broadcastProcessor = new BroadcastActivityProcessor(new[] { processor1, processor2 });

            broadcastProcessor.ShutdownAsync(default);
            Assert.True(processor1.ShutdownCalled);
            Assert.True(processor2.ShutdownCalled);

            broadcastProcessor.Dispose();
            Assert.True(processor1.DisposedCalled);
            Assert.True(processor2.DisposedCalled);
        }

        private class TestProcessor : ActivityProcessor, IDisposable
        {
            private readonly Action<Activity> onStart;
            private readonly Action<Activity> onEnd;

            public TestProcessor(Action<Activity> onStart, Action<Activity> onEnd)
            {
                this.onStart = onStart;
                this.onEnd = onEnd;
            }

            public bool ShutdownCalled { get; private set; } = false;

            public bool DisposedCalled { get; private set; } = false;

            public override void OnStart(Activity span)
            {
                this.onStart?.Invoke(span);
            }

            public override void OnEnd(Activity span)
            {
                this.onEnd?.Invoke(span);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                this.ShutdownCalled = true;
#if NET452
                return Task.FromResult(0);
#else
                return Task.CompletedTask;
#endif
            }

            public void Dispose()
            {
                this.DisposedCalled = true;
            }
        }
    }
}
