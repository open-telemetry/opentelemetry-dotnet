// <copyright file="BatchExportProcessorOptionsTest.cs" company="OpenTelemetry Authors">
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

using Options = OpenTelemetry.BatchExportProcessorOptions<object>;

namespace OpenTelemetry.Trace.Tests
{
    public class BatchExportProcessorOptionsTest : IDisposable
    {
        public BatchExportProcessorOptionsTest()
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
            var options = new Options();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(Options.ExporterTimeoutEnvVarKey, "1");
            Environment.SetEnvironmentVariable(Options.MaxExportBatchSizeEnvVarKey, "2");
            Environment.SetEnvironmentVariable(Options.MaxQueueSizeEnvVarKey, "3");
            Environment.SetEnvironmentVariable(Options.ScheduledDelayEnvVarKey, "4");

            var options = new BatchExportProcessorOptions<object>();

            Assert.Equal(1, options.ExporterTimeoutMilliseconds);
            Assert.Equal(2, options.MaxExportBatchSize);
            Assert.Equal(3, options.MaxQueueSize);
            Assert.Equal(4, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidPortEnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<object>.ExporterTimeoutEnvVarKey, "invalid");

            var options = new Options();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds); // use default
        }

        [Fact]
        public void BatchExportProcessorOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(BatchExportProcessorOptions<object>.ExporterTimeoutEnvVarKey, "123");

            var options = new Options
            {
                ExporterTimeoutMilliseconds = 89000,
            };

            Assert.Equal(89000, options.ExporterTimeoutMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_BSP_EXPORT_TIMEOUT", Options.ExporterTimeoutEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", Options.MaxExportBatchSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_QUEUE_SIZE", Options.MaxQueueSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_SCHEDULE_DELAY", Options.ScheduledDelayEnvVarKey);
        }

        private void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(Options.ExporterTimeoutEnvVarKey, null);
            Environment.SetEnvironmentVariable(Options.MaxExportBatchSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(Options.MaxQueueSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(Options.ScheduledDelayEnvVarKey, null);
        }
    }
}
