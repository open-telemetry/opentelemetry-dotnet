// <copyright file="BatchExportLogRecordProcessorOptionsTest.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class BatchExportLogRecordProcessorOptionsTest : IDisposable
{
    public BatchExportLogRecordProcessorOptionsTest()
    {
        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BatchExportLogRecordProcessorOptions_Defaults()
    {
        var options = new BatchExportLogRecordProcessorOptions();

        Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
        Assert.Equal(512, options.MaxExportBatchSize);
        Assert.Equal(2048, options.MaxQueueSize);
        Assert.Equal(5000, options.ScheduledDelayMilliseconds);
    }

    [Fact]
    public void BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride()
    {
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.ExporterTimeoutEnvVarKey, "1");
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.MaxExportBatchSizeEnvVarKey, "2");
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.MaxQueueSizeEnvVarKey, "3");
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.ScheduledDelayEnvVarKey, "4");

        var options = new BatchExportLogRecordProcessorOptions();

        Assert.Equal(1, options.ExporterTimeoutMilliseconds);
        Assert.Equal(2, options.MaxExportBatchSize);
        Assert.Equal(3, options.MaxQueueSize);
        Assert.Equal(4, options.ScheduledDelayMilliseconds);
    }

    [Fact]
    public void ExportLogRecordProcessorOptions_UsingIConfiguration()
    {
        var values = new Dictionary<string, string>()
        {
            [BatchExportLogRecordProcessorOptions.MaxQueueSizeEnvVarKey] = "1",
            [BatchExportLogRecordProcessorOptions.MaxExportBatchSizeEnvVarKey] = "2",
            [BatchExportLogRecordProcessorOptions.ExporterTimeoutEnvVarKey] = "3",
            [BatchExportLogRecordProcessorOptions.ScheduledDelayEnvVarKey] = "4",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new BatchExportLogRecordProcessorOptions(configuration);

        Assert.Equal(1, options.MaxQueueSize);
        Assert.Equal(2, options.MaxExportBatchSize);
        Assert.Equal(3, options.ExporterTimeoutMilliseconds);
        Assert.Equal(4, options.ScheduledDelayMilliseconds);
    }

    [Fact]
    public void BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.ExporterTimeoutEnvVarKey, "123");

        var options = new BatchExportLogRecordProcessorOptions
        {
            ExporterTimeoutMilliseconds = 89000,
        };

        Assert.Equal(89000, options.ExporterTimeoutMilliseconds);
    }

    [Fact]
    public void BatchExportLogRecordProcessorOptions_EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_BLRP_EXPORT_TIMEOUT", BatchExportLogRecordProcessorOptions.ExporterTimeoutEnvVarKey);
        Assert.Equal("OTEL_BLRP_MAX_EXPORT_BATCH_SIZE", BatchExportLogRecordProcessorOptions.MaxExportBatchSizeEnvVarKey);
        Assert.Equal("OTEL_BLRP_MAX_QUEUE_SIZE", BatchExportLogRecordProcessorOptions.MaxQueueSizeEnvVarKey);
        Assert.Equal("OTEL_BLRP_SCHEDULE_DELAY", BatchExportLogRecordProcessorOptions.ScheduledDelayEnvVarKey);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.ExporterTimeoutEnvVarKey, null);
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.MaxExportBatchSizeEnvVarKey, null);
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.MaxQueueSizeEnvVarKey, null);
        Environment.SetEnvironmentVariable(BatchExportLogRecordProcessorOptions.ScheduledDelayEnvVarKey, null);
    }
}
