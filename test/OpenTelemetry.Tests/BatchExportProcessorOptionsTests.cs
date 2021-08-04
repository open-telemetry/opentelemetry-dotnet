// <copyright file="BatchExportProcessorOptionsTests.cs" company="OpenTelemetry Authors">
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
    public class BatchExportProcessorOptionsTests : IDisposable
    {
        public BatchExportProcessorOptionsTests()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
        }

        [Fact]
        public void BatchExportProcessorOptions_Defaults()
        {
            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ExportTimeoutEnvVarName, "100");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxExportBatchSizeEnvVarName, "200");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxQueueSizeEnvVarName, "300");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ScheduleDelayEnvVarName, "400");

            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(100, options.ExporterTimeoutMilliseconds);
            Assert.Equal(200, options.MaxExportBatchSize);
            Assert.Equal(300, options.MaxQueueSize);
            Assert.Equal(400, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidExporterTimeoutMillisecondsOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ExportTimeoutEnvVarName, "invalid");

            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidMaxExportBatchSizeOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxExportBatchSizeEnvVarName, "invalid");

            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(512, options.MaxExportBatchSize); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidMaxQueueSizeOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxQueueSizeEnvVarName, "invalid");

            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(2048, options.MaxQueueSize); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidScheduledDelayMillisecondsOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ScheduleDelayEnvVarName, "invalid");

            var options = new BatchExportProcessorOptions<TestClass>();

            Assert.Equal(5000, options.ScheduledDelayMilliseconds); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ExportTimeoutEnvVarName, "100");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxExportBatchSizeEnvVarName, "200");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxQueueSizeEnvVarName, "300");
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ScheduleDelayEnvVarName, "400");

            var options = new BatchExportProcessorOptions<TestClass>
            {
                ExporterTimeoutMilliseconds = 10,
                MaxExportBatchSize = 20,
                MaxQueueSize = 30,
                ScheduledDelayMilliseconds = 40,
            };

            Assert.Equal(10, options.ExporterTimeoutMilliseconds);
            Assert.Equal(20, options.MaxExportBatchSize);
            Assert.Equal(30, options.MaxQueueSize);
            Assert.Equal(40, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_BSP_EXPORT_TIMEOUT", BatchExportProcessorOptions<TestClass>.ExportTimeoutEnvVarName);
            Assert.Equal("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", BatchExportProcessorOptions<TestClass>.MaxExportBatchSizeEnvVarName);
            Assert.Equal("OTEL_BSP_MAX_QUEUE_SIZE", BatchExportProcessorOptions<TestClass>.MaxQueueSizeEnvVarName);
            Assert.Equal("OTEL_BSP_SCHEDULE_DELAY", BatchExportProcessorOptions<TestClass>.ScheduleDelayEnvVarName);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ExportTimeoutEnvVarName, null);
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxExportBatchSizeEnvVarName, null);
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.MaxQueueSizeEnvVarName, null);
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<TestClass>.ScheduleDelayEnvVarName, null);
        }

        private class TestClass
        {
        }
    }
}
