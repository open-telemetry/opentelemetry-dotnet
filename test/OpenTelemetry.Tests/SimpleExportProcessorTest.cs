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
        public void Verify_OnExport_HandlesException()
        {
            var exceptionTestExporter = new ExceptionTestExporter();
            var testSimpleExportProcessor = new TestSimpleExportProcessor(exceptionTestExporter);

            // verify that this does NOT throw.
            testSimpleExportProcessor.InvokeOnExport();
        }

        public class TestSimpleExportProcessor : SimpleExportProcessor<object>
        {
            public TestSimpleExportProcessor(BaseExporter<object> exporter)
                : base(exporter)
            {
            }

            public void InvokeOnExport() => this.OnExport(new object());
        }

        /// <summary>
        /// This Exporter overrides the <see cref="Export(in Batch{object})"/> to throw an exception.
        /// This will test that exceptions are caught and handled by <see cref="SimpleExportProcessor{T}"/>.
        /// </summary>
        public class ExceptionTestExporter : BaseExporter<object>
        {
            public override ExportResult Export(in Batch<object> batch)
            {
                throw new Exception("test exception");
            }
        }
    }
}
