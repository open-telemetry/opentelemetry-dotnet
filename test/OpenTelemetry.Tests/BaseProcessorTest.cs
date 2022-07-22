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
            var testExporter = new TestProcessor();
            Assert.True(testExporter.ForceFlush());

            // BaseExporter should catch any exceptions and return false.
            var exceptionTestExporter = new ExceptionTestProcessor();
            Assert.False(exceptionTestExporter.ForceFlush());
        }

        [Fact]
        public void Verify_Shutdown_HandlesSecond()
        {
            // By default, Shutdown should return true.
            var testExporter = new TestProcessor();
            Assert.True(testExporter.Shutdown());

            // A second Shutdown should return false.
            Assert.False(testExporter.Shutdown());
        }

        [Fact]
        public void Verify_Shutdown_HandlesException()
        {
            // BaseExporter should catch any exceptions and return false.
            var exceptionTestExporter = new ExceptionTestProcessor();
            Assert.False(exceptionTestExporter.Shutdown());
        }

        /// <summary>
        /// This Exporter will be used to test the default behavior of <see cref="BaseProcessor{T}"/>.
        /// </summary>
        public class TestProcessor : BaseProcessor<object>
        {
        }

        /// <summary>
        /// This Exporter overrides the <see cref="OnForceFlush(int)"/> and <see cref="OnShutdown(int)"/>
        /// methods to throw an exception. This will test that exceptions are caught and handled by
        /// the <see cref="BaseProcessor{T}"/>.
        /// </summary>
        public class ExceptionTestProcessor : BaseProcessor<object>
        {
            protected override bool OnForceFlush(int timeoutMilliseconds)
            {
                throw new Exception("test exception");
            }

            protected override bool OnShutdown(int timeoutMilliseconds)
            {
                throw new Exception("test exception");
            }
        }
    }
}
