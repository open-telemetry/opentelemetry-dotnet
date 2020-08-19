﻿// <copyright file="CompositeActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
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
            var result = string.Empty;

            using var p1 = new TestActivityProcessor(
                activity => { result += "1"; },
                activity => { result += "3"; });
            using var p2 = new TestActivityProcessor(
                activity => { result += "2"; },
                activity => { result += "4"; });

            var activity = new Activity("test");

            using (var processor = new CompositeActivityProcessor(new[] { p1, p2 }))
            {
                processor.OnStart(activity);
                processor.OnEnd(activity);
            }

            Assert.Equal("1234", result);
        }

        [Fact]
        public void CompositeActivityProcessor_ProcessorThrows()
        {
            using var p1 = new TestActivityProcessor(
                activity => { throw new Exception("Start exception"); },
                activity => { throw new Exception("End exception"); });

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
            using var p1 = new TestActivityProcessor(null, null);
            using var p2 = new TestActivityProcessor(null, null);

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
            using var p1 = new TestActivityProcessor(null, null);
            using var p2 = new TestActivityProcessor(null, null);

            using (var processor = new CompositeActivityProcessor(new[] { p1, p2 }))
            {
                processor.ForceFlushAsync(default).Wait();
                Assert.True(p1.ForceFlushCalled);
                Assert.True(p2.ForceFlushCalled);
            }
        }
    }
}
