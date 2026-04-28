// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public sealed class BatchExportActivityProcessorOptionsTests
{
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
        using (EnvironmentVariableScope.Create(
            [
                (BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "1"),
                (BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey, "2"),
                (BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "3"),
                (BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey, "4"),
            ]))
        {
            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(1, options.ExporterTimeoutMilliseconds);
            Assert.Equal(2, options.MaxExportBatchSize);
            Assert.Equal(3, options.MaxQueueSize);
            Assert.Equal(4, options.ScheduledDelayMilliseconds);
        }
    }

    [Fact]
    public void BatchExportProcessorOptions_UsingIConfiguration()
    {
        var values = new Dictionary<string, string?>()
        {
            [BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey] = "3",
            [BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey] = "2",
            [BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey] = "3",
            [BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey] = "4",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new BatchExportActivityProcessorOptions(configuration);

        Assert.Equal(3, options.MaxQueueSize);
        Assert.Equal(2, options.MaxExportBatchSize);
        Assert.Equal(3, options.ExporterTimeoutMilliseconds);
        Assert.Equal(4, options.ScheduledDelayMilliseconds);
    }

    [Fact]
    public void BatchExportProcessorOptions_InvalidEnvironmentVariableOverride()
    {
        using (EnvironmentVariableScope.Create(
            [
                (BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "invalid"),
                (BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey, "invalid"),
                (BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "invalid"),
                (BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey, "invalid"),
            ]))
        {
            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(30000, options.ExporterTimeoutMilliseconds);
            Assert.Equal(512, options.MaxExportBatchSize);
            Assert.Equal(2048, options.MaxQueueSize);
            Assert.Equal(5000, options.ScheduledDelayMilliseconds);
        }
    }

    [Fact]
    public void BatchExportProcessorOptions_InvalidNumericEnvironmentVariableOverrideFallsBackToDefaults()
    {
        using (EnvironmentVariableScope.Create(BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "0"))
        {
            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(2048, options.MaxQueueSize);
        }
    }

    [Fact]
    public void BatchExportProcessorOptions_MaxQueueSizeLowerThanEffectiveMaxExportBatchSizeClampsBatchSize()
    {
        using (EnvironmentVariableScope.Create(BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey, "10"))
        {
            var options = new BatchExportActivityProcessorOptions();

            Assert.Equal(10, options.MaxQueueSize);
            Assert.Equal(10, options.MaxExportBatchSize);
        }
    }

    [Fact]
    public void BatchExportProcessorOptions_SetterOverridesEnvironmentVariable()
    {
        using (EnvironmentVariableScope.Create(BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey, "123"))
        {
            var options = new BatchExportActivityProcessorOptions
            {
                ExporterTimeoutMilliseconds = 89000,
            };

            Assert.Equal(89000, options.ExporterTimeoutMilliseconds);
        }
    }

    [Fact]
    public void BatchExportProcessorOptions_EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_BSP_EXPORT_TIMEOUT", BatchExportActivityProcessorOptions.ExporterTimeoutEnvVarKey);
        Assert.Equal("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", BatchExportActivityProcessorOptions.MaxExportBatchSizeEnvVarKey);
        Assert.Equal("OTEL_BSP_MAX_QUEUE_SIZE", BatchExportActivityProcessorOptions.MaxQueueSizeEnvVarKey);
        Assert.Equal("OTEL_BSP_SCHEDULE_DELAY", BatchExportActivityProcessorOptions.ScheduledDelayEnvVarKey);
    }
}
