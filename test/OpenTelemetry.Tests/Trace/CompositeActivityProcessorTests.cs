// <copyright file="CompositeActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests.Shared;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class CompositeActivityProcessorTests
    {
        [Fact]
        public void CompositeActivityProcessor_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => new CompositeActivityProcessor(null));
            Assert.Throws<ArgumentException>(() => new CompositeActivityProcessor(new SimpleActivityProcessor[0]));
        }

        [Fact]
        public void CompositeActivityProcessor_CallsAllProcessorSequentially()
        {
            bool start1Called = false;
            bool start2Called = false;
            bool end1Called = false;
            bool end2Called = false;
            var processor1 = new TestActivityProcessor(
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
            var processor2 = new TestActivityProcessor(
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

            var compositeActivityProcessor = new CompositeActivityProcessor(new[] { processor1, processor2 });

            var activity = new Activity("somename");
            compositeActivityProcessor.OnStart(activity);
            Assert.True(start1Called);
            Assert.True(start2Called);

            compositeActivityProcessor.OnEnd(activity);
            Assert.True(end1Called);
            Assert.True(end2Called);
        }

        [Fact]
        public void CompositeActivityProcessor_ProcessorThrows()
        {
            var p1 = new TestActivityProcessor(
                ss => { throw new Exception("Start exception"); },
                se => { throw new Exception("End exception"); });

            var activity = new Activity("test");

            using (var processor = new CompositeActivityProcessor(new[] { p1 }))
            {
                Assert.Throws<Exception>(() => { processor.OnStart(activity); });
                Assert.Throws<Exception>(() => { processor.OnEnd(activity); });
            }
        }

        [Fact]
        public void CompositeActivityProcessor_ShutsDownAll()
        {
            var p1 = new TestActivityProcessor(null, null);
            var p2 = new TestActivityProcessor(null, null);

            using (var processor = new CompositeActivityProcessor(new[] { p1, p2 }))
            {
                processor.ShutdownAsync(default).Wait();
                Assert.True(p1.ShutdownCalled);
                Assert.True(p2.ShutdownCalled);
            }
        }

        [Fact]
        public void CompositeActivityProcessor_ForceFlush()
        {
            var p1 = new TestActivityProcessor(null, null);
            var p2 = new TestActivityProcessor(null, null);

            using (var processor = new CompositeActivityProcessor(new[] { p1, p2 }))
            {
                processor.ForceFlushAsync(default).Wait();
                Assert.True(p1.ForceFlushCalled);
                Assert.True(p2.ForceFlushCalled);
            }
        }
    }
}
