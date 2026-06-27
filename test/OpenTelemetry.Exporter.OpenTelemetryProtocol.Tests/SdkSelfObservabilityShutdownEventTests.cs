// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CA2000 // Provider takes ownership of processor/exporter

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class SdkSelfObservabilityShutdownEventTests
{
    [Fact]
    public void Shutdown_LoggerProvider_EmitsProviderLevelEvent()
    {
        var selfObsRecords = new List<LogRecord>();
        using var selfObsLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddInMemoryExporter(selfObsRecords);
            });
        });
        var selfObsLogger = selfObsLoggerFactory.CreateLogger("OpenTelemetry.SDK");

        SdkSelfObservability.SetLogger(selfObsLogger);
        try
        {
            var userProvider = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(new List<LogRecord>())))
                .Build();

            userProvider.Shutdown();

            // Clear the sink BEFORE disposing selfObsLoggerFactory, otherwise
            // the self-obs LoggerProvider's own shutdown event gets captured.
            SdkSelfObservability.SetLogger(null);
            selfObsLoggerFactory.Dispose();
        }
        finally
        {
            SdkSelfObservability.SetLogger(null);
        }

        Assert.Single(selfObsRecords);
        var record = selfObsRecords[0];

        Assert.Equal("otel.sdk.component.shutdown", record.EventId.Name);
#pragma warning disable CS0618
        Assert.Equal(LogLevel.Information, record.LogLevel);
#pragma warning restore CS0618
        Assert.Equal("logger_provider", GetAttribute<string>(record, "otel.component.type"));
        Assert.Null(record.Attributes!.FirstOrDefault(kv => kv.Key == "error.type").Value);

        var duration = GetAttribute<double>(record, "otel.component.shutdown.duration");
        Assert.True(duration >= 0.0);
    }

    [Fact]
    public void Shutdown_NoLoggerConfigured_IsNoOp()
    {
        SdkSelfObservability.SetLogger(null);

        var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(new List<LogRecord>())))
            .Build();

        provider.Dispose();
    }

    [Fact]
    public void Shutdown_CalledTwice_EmitsOnlyOneEvent()
    {
        var selfObsRecords = new List<LogRecord>();
        using var selfObsLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddInMemoryExporter(selfObsRecords));
        });
        SdkSelfObservability.SetLogger(selfObsLoggerFactory.CreateLogger("OpenTelemetry.SDK"));
        try
        {
            var provider = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(new List<LogRecord>())))
                .Build();

            Assert.True(provider.Shutdown());
            Assert.False(provider.Shutdown());

            SdkSelfObservability.SetLogger(null);
            selfObsLoggerFactory.Dispose();
        }
        finally
        {
            SdkSelfObservability.SetLogger(null);
        }

        Assert.Single(selfObsRecords);
    }

    private static T GetAttribute<T>(LogRecord record, string key)
    {
        Assert.NotNull(record.Attributes);
        var match = record.Attributes!.FirstOrDefault(kv => kv.Key == key);
        Assert.NotEqual(default, match);
        Assert.IsType<T>(match.Value);
        return (T)match.Value!;
    }
}
