// <copyright file="BaseProcessorTest.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Tests
{
    public class BaseProcessorTest
    {
        [Fact]
        public void Verify_ForceFlush_HandlesException()
        {
            // By default, ForceFlush should return true.
            var testProcessor = new DelegatingProcessor<object>();
            Assert.True(testProcessor.ForceFlush());

            // BaseExporter should catch any exceptions and return false.
            testProcessor.OnForceFlushFunc = (timeout) => throw new Exception("test exception");
            Assert.False(testProcessor.ForceFlush());
        }

        [Fact]
        public void Verify_Shutdown_HandlesSecond()
        {
            // By default, Shutdown should return true.
            var testProcessor = new DelegatingProcessor<object>();
            Assert.True(testProcessor.Shutdown());

            // A second Shutdown should return false.
            Assert.False(testProcessor.Shutdown());
        }

        [Fact]
        public void Verify_Shutdown_HandlesException()
        {
            // BaseExporter should catch any exceptions and return false.
            var exceptionTestProcessor = new DelegatingProcessor<object>
            {
                OnShutdownFunc = (timeout) => throw new Exception("test exception"),
            };
            Assert.False(exceptionTestProcessor.Shutdown());
        }

        [Fact]
        public void NoOp()
        {
            var testProcessor = new DelegatingProcessor<object>();

            // These two methods are no-op, but account for 7% of the test coverage.
            testProcessor.OnStart(new object());
            testProcessor.OnEnd(new object());
        }
    }
}
