// <copyright file="BatchExportActivityProcessorOptionsTest.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class BatchExportActivityProcessorOptionsTest : IDisposable
    {
        public BatchExportActivityProcessorOptionsTest()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void BatchExportProcessorOptions_Defaults()
        {
            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "1");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey, "2");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "3");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey, "4");

            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(1, options.ExporterTimeoutMilliseconds);
            Assert.Equal(2, options.MaxExportBatchSize);
            Assert.Equal(3, options.MaxQueueSize);
            Assert.Equal(4, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_UsingIConfiguration()
        {
            var values = new Dictionary<string, string>()
            {
                [BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey] = "1",
                [BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey] = "2",
                [BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey] = "3",
                [BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey] = "4",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var options = new BatchExportActivityProcessorOptions(configuration);

            Assert.Equal(1, options.MaxQueueSize);
            Assert.Equal(2, options.MaxExportBatchSize);
            Assert.Equal(3, options.ExporterTimeoutMilliseconds);
            Assert.Equal(4, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_InvalidEnvironmentVariableOverride()
        {
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "invalid");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey, "invalid");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "invalid");
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey, "invalid");

            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_SetterOverridesEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "123");

            var options = new BatchExportActivityProcessorOptions
            {
                ExporterTimeoutMilliseconds = 89000,
            };

            Assert.Equal(89000, options.ExporterTimeoutMilliseconds);
        }

        [Fact]
        public void BatchExportProcessorOptions_EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_BSP_EXPORT_TIMEOUT", BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_MAX_QUEUE_SIZE", BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey);
            Assert.Equal("OTEL_BSP_SCHEDULE_DELAY", BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, null);
            Environment.SetEnvironmentVariable(BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey, null);
        }
    }
}
