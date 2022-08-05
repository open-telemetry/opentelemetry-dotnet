// <copyright file="SimpleExportProcessorTest.cs" company="OpenTelemetry Authors">
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
    public class SimpleExportProcessorTest
    {
        [Fact]
        public void Verify_SimpleExportProcessor_HandlesException()
        {
            int counter = 0;

            // here our exporter will throw an exception.
            var testExporter = new DelegatingExporter<object>
            {
                OnExportFunc = (batch) =>
                {
                    counter++;
                    throw new Exception("test exception");
                },
            };

            var testSimpleExportProcessor = new TestSimpleExportProcessor(testExporter);

            // Verify that the Processor catches and suppresses the exception.
            testSimpleExportProcessor.OnEnd(new object());

            // verify Exporter OnExport wall called.
            Assert.Equal(1, counter);
        }

        /// <summary>
        /// Testable class for abstract <see cref="SimpleExportProcessor{T}"/>.
        /// </summary>
        public class TestSimpleExportProcessor : SimpleExportProcessor<object>
        {
            public TestSimpleExportProcessor(BaseExporter<object> exporter)
                : base(exporter)
            {
            }
        }
    }
}
