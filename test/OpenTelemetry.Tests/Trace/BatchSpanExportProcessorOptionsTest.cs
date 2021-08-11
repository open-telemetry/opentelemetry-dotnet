// <copyright file="BatchSpanExportProcessorOptionsTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Tests
{
    public class BatchSpanExportProcessorOptionsTest : IDisposable
    {
        public BatchSpanExportProcessorOptionsTest()
        {
            this.ClearEnvVars();
        }

        public void Dispose()
        {
            this.ClearEnvVars();
        }

        [Fact]
        public void BatchExportProcessorOptions_Defaults()
        {
            var options = new BatchSpanExportProcessorOptions();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ExporterTimeoutEnvVarKey, "1");
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.MaxExportBatchSizeEnvVarKey, "2");
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.MaxQueueSizeEnvVarKey, "3");
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ScheduledDelayEnvVarKey, "4");

            var options = new BatchSpanExportProcessorOptions();

            Assert.Equal(1, options.ExporterTimeoutMilliseconds);
            Assert.Equal(2, options.MaxExportBatchSize);
            Assert.Equal(3, options.MaxQueueSize);
            Assert.Equal(4, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidPortEnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ExporterTimeoutEnvVarKey, "invalid");

            var options = new BatchSpanExportProcessorOptions();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ExporterTimeoutEnvVarKey, "123");

            var options = new BatchSpanExportProcessorOptions
            {
                ExporterTimeoutMilliseconds = 89000,
            };

            Assert.Equal(89000, options.ExporterTimeoutMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_BSP_EXPORT_TIMEOUT", BatchSpanExportProcessorOptions.ExporterTimeoutEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", BatchSpanExportProcessorOptions.MaxExportBatchSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_QUEUE_SIZE", BatchSpanExportProcessorOptions.MaxQueueSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_SCHEDULE_DELAY", BatchSpanExportProcessorOptions.ScheduledDelayEnvVarKey);
        }

        private void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ExporterTimeoutEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.MaxExportBatchSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.MaxQueueSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchSpanExportProcessorOptions.ScheduledDelayEnvVarKey, null);
        }
    }
}
