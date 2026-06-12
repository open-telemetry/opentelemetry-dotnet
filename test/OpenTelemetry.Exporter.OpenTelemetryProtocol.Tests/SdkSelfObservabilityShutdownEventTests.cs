// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// POC verification for the otel.sdk.component.shutdown event
/// (semantic-conventions#3723) wired through BatchLogRecordExportProcessor
/// and OtlpLogExporter.
/// </summary>
/// <remarks>
/// Test discipline: process-global state on
/// <see cref="SdkSelfObservability"/> means these tests must restore the
/// sink in <c>finally</c>. With xunit's serial default (set in
/// build/xunit.runner.json) cross-test interference is not a concern but
/// the restore keeps the discipline correct.
/// </remarks>
public class SdkSelfObservabilityShutdownEventTests
{
    [Fact]
    public void Shutdown_BatchLogRecordExportProcessor_Wrapping_OtlpLogExporter_EmitsBothEventsInInnerFirstOrder()
    {
        // Self-observability sink: a separate LoggerProvider with an
        // InMemoryExporter. MUST be a different provider from the one
        // emitting the events (the one being shut down).
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
            // Build a "user" LoggerProvider whose BLP wraps a real
            // OtlpLogExporter (with a no-op transmission client so the test
            // doesn't actually hit the network). Disposing this provider
            // triggers the shutdown sequence we want to observe.
            var testExportClient = new TestExportClient();
            var exporterOptions = new OtlpExporterOptions();
            using var transmissionHandler = new OtlpExporterTransmissionHandler(
                testExportClient,
                exporterOptions.TimeoutMilliseconds);

            using var exporter = new OtlpLogExporter(
                exporterOptions,
                new SdkLimitOptions(),
                new ExperimentalOptions(),
                transmissionHandler);

            using var processor = new BatchLogRecordExportProcessor(exporter);

            var userProvider = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(processor)
                .Build();

            // Disposing the provider calls Shutdown on its processors,
            // which calls Shutdown on the underlying exporter. Both should
            // emit one otel.sdk.component.shutdown event.
            userProvider.Dispose();

            // Drain the self-obs provider so its BLP flushes the events.
            selfObsLoggerFactory.Dispose();
        }
        finally
        {
            SdkSelfObservability.SetLogger(null);
        }

        Assert.Equal(2, selfObsRecords.Count);

        // Spec PR open-telemetry/semantic-conventions#3723 (after the
        // tightening commit 2a9ea860): the parent's duration mechanically
        // includes the child's; the child (exporter) emits first, the
        // parent (processor) second.
        var exporterRecord = selfObsRecords[0];
        var processorRecord = selfObsRecords[1];

        AssertShutdownEventShape(
            exporterRecord,
            expectedComponentType: "otlp_grpc_log_exporter",
            expectedComponentNamePrefix: "otlp_grpc_log_exporter/",
            expectedResult: "success",
            expectedLevel: LogLevel.Information);

        AssertShutdownEventShape(
            processorRecord,
            expectedComponentType: "batching_log_processor",
            expectedComponentNamePrefix: "batching_log_processor/",
            expectedResult: "success",
            expectedLevel: LogLevel.Information);

        // Spec note: parent duration includes inner duration. Sanity:
        // processor.duration >= exporter.duration (allow equality for the
        // sub-microsecond no-op case).
        var exporterDuration = GetAttribute<double>(exporterRecord, "otel.component.shutdown.duration");
        var processorDuration = GetAttribute<double>(processorRecord, "otel.component.shutdown.duration");
        Assert.True(
            processorDuration >= exporterDuration,
            $"Expected processor.duration ({processorDuration}) >= exporter.duration ({exporterDuration})");
    }

    [Fact]
    public void Shutdown_NoLoggerConfigured_IsNoOp()
    {
        SdkSelfObservability.SetLogger(null);

        var exporterOptions = new OtlpExporterOptions();
        using var transmissionHandler = new OtlpExporterTransmissionHandler(
            new TestExportClient(),
            exporterOptions.TimeoutMilliseconds);
        using var exporter = new OtlpLogExporter(
            exporterOptions,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            transmissionHandler);
        using var processor = new BatchLogRecordExportProcessor(exporter);
        var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(processor)
            .Build();

        // No assertions other than "this does not throw". The contract is
        // that EmitComponentShutdown short-circuits when no logger is set.
        provider.Dispose();
    }

    [Fact]
    public void Shutdown_CalledTwice_EmitsOnlyOneEventPerComponent()
    {
        var selfObsRecords = new List<LogRecord>();
        using var selfObsLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddInMemoryExporter(selfObsRecords));
        });
        SdkSelfObservability.SetLogger(selfObsLoggerFactory.CreateLogger("OpenTelemetry.SDK"));
        try
        {
            var exporterOptions = new OtlpExporterOptions();
            using var transmissionHandler = new OtlpExporterTransmissionHandler(
                new TestExportClient(),
                exporterOptions.TimeoutMilliseconds);
            using var exporter = new OtlpLogExporter(
                exporterOptions,
                new SdkLimitOptions(),
                new ExperimentalOptions(),
                transmissionHandler);

            using var processor = new BatchLogRecordExportProcessor(exporter);

            // BaseProcessor.Shutdown and BaseExporter.Shutdown are guarded
            // by Interlocked counters, so OnShutdown is invoked at most
            // once -- the spec's "MUST NOT emit additional events on
            // subsequent shutdown invocations" rule comes for free.
            Assert.True(processor.Shutdown());
            Assert.False(processor.Shutdown());
            Assert.False(processor.Shutdown());

            selfObsLoggerFactory.Dispose();
        }
        finally
        {
            SdkSelfObservability.SetLogger(null);
        }

        Assert.Equal(2, selfObsRecords.Count);
    }

    private static void AssertShutdownEventShape(
        LogRecord record,
        string expectedComponentType,
        string expectedComponentNamePrefix,
        string expectedResult,
        LogLevel expectedLevel)
    {
        Assert.Equal("otel.sdk.component.shutdown", record.EventId.Name);
#pragma warning disable CS0618 // LogRecord.LogLevel obsolete; POC test asserts on it intentionally.
        Assert.Equal(expectedLevel, record.LogLevel);
#pragma warning restore CS0618

        Assert.Equal(expectedComponentType, GetAttribute<string>(record, "otel.component.type"));
        var name = GetAttribute<string>(record, "otel.component.name");
        Assert.StartsWith(expectedComponentNamePrefix, name, StringComparison.Ordinal);
        Assert.Equal(expectedResult, GetAttribute<string>(record, "otel.component.shutdown.result"));

        var duration = GetAttribute<double>(record, "otel.component.shutdown.duration");
        Assert.True(duration >= 0.0, $"duration was negative: {duration}");
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
