// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class IntegrationTests : IDisposable
{
    private const string CollectorHostnameEnvVarName = "OTEL_COLLECTOR_HOSTNAME";
    private const int ExportIntervalMilliseconds = 10000;
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();
    private static readonly ExperimentalOptions DefaultExperimentalOptions = new();
    private static readonly string? CollectorHostname = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(CollectorHostnameEnvVarName);
    private readonly OpenTelemetryEventListener openTelemetryEventListener;

    public IntegrationTests(ITestOutputHelper outputHelper)
    {
        this.openTelemetryEventListener = new(outputHelper);
    }

    public void Dispose()
    {
        this.openTelemetryEventListener.Dispose();
    }

    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Batch, false)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces", ExportProcessorType.Batch, false)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Batch, true)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces", ExportProcessorType.Batch, true)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Simple, false)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces", ExportProcessorType.Simple, false)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Simple, true)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/traces", ExportProcessorType.Simple, true)]
    [InlineData(OtlpExportProtocol.Grpc, ":5317", ExportProcessorType.Simple, true, "https")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":5318/v1/traces", ExportProcessorType.Simple, true, "https")]
    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    public void TraceExportResultIsSuccess(OtlpExportProtocol protocol, string endpoint, ExportProcessorType exportProcessorType, bool forceFlush, string scheme = "http")
    {
        using EventWaitHandle handle = new ManualResetEvent(false);

        var exporterOptions = new OtlpExporterOptions
        {
            Endpoint = new Uri($"{scheme}://{CollectorHostname}{endpoint}"),
            Protocol = protocol,
            ExportProcessorType = exportProcessorType,
            BatchExportProcessorOptions = new()
            {
                ScheduledDelayMilliseconds = ExportIntervalMilliseconds,
            },
        };

        DelegatingExporter<Activity>? delegatingExporter = null;
        var exportResults = new List<ExportResult>();

        var activitySourceName = "otlp.collector.test";

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName);

        builder.AddProcessor(sp => OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
            serviceProvider: sp,
            exporterOptions: exporterOptions,
            sdkLimitOptions: DefaultSdkLimitOptions,
            experimentalOptions: DefaultExperimentalOptions,
            configureExporterInstance: otlpExporter =>
            {
                delegatingExporter = new DelegatingExporter<Activity>
                {
                    OnExportFunc = (batch) =>
                    {
                        var result = otlpExporter.Export(batch);
                        exportResults.Add(result);
                        handle.Set();
                        return result;
                    },
                };
                return delegatingExporter;
            }));

        using (var tracerProvider = builder.Build())
        {
            using var source = new ActivitySource(activitySourceName);
            var activity = source.StartActivity($"{protocol} Test Activity");
            activity?.Stop();

            Assert.NotNull(delegatingExporter);

            if (forceFlush)
            {
                Assert.True(tracerProvider.ForceFlush());
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
            }
            else if (exporterOptions.ExportProcessorType == ExportProcessorType.Batch)
            {
                Assert.True(handle.WaitOne(ExportIntervalMilliseconds * 2));
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
            }
        }

        if (!forceFlush && exportProcessorType == ExportProcessorType.Simple)
        {
            Assert.Single(exportResults);
            Assert.Equal(ExportResult.Success, exportResults[0]);
        }
    }

    [InlineData(OtlpExportProtocol.Grpc, ":4317", false, false)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/metrics", false, false)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", false, true)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/metrics", false, true)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", true, false)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/metrics", true, false)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", true, true)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/metrics", true, true)]
    [InlineData(OtlpExportProtocol.Grpc, ":5317", true, true, "https")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":5318/v1/metrics", true, true, "https")]
    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    public void MetricExportResultIsSuccess(OtlpExportProtocol protocol, string endpoint, bool useManualExport, bool forceFlush, string scheme = "http")
    {
        using EventWaitHandle handle = new ManualResetEvent(false);

        var exporterOptions = new OtlpExporterOptions
        {
            Endpoint = new Uri($"{scheme}://{CollectorHostname}{endpoint}"),
            Protocol = protocol,
        };

        DelegatingExporter<Metric>? delegatingExporter = null;
        var exportResults = new List<ExportResult>();

        var meterName = "otlp.collector.test";

        var builder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName);

        var readerOptions = new MetricReaderOptions();
        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = useManualExport ? Timeout.Infinite : ExportIntervalMilliseconds;

        builder.AddReader(sp => OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
            serviceProvider: sp,
            exporterOptions: exporterOptions,
            metricReaderOptions: readerOptions,
            experimentalOptions: DefaultExperimentalOptions,
            configureExporterInstance: otlpExporter =>
            {
                delegatingExporter = new DelegatingExporter<Metric>
                {
                    OnExportFunc = (batch) =>
                    {
                        var result = otlpExporter.Export(batch);
                        exportResults.Add(result);
                        handle.Set();
                        return result;
                    },
                };
                return delegatingExporter;
            }));

        using (var meterProvider = builder.Build())
        {
            using var meter = new Meter(meterName);

            var counter = meter.CreateCounter<int>("test_counter");

            counter.Add(18);

            Assert.NotNull(delegatingExporter);

            if (forceFlush)
            {
                Assert.True(meterProvider.ForceFlush());
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
            }
            else if (!useManualExport)
            {
                Assert.True(handle.WaitOne(ExportIntervalMilliseconds * 2));
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
            }
        }

        if (!forceFlush && useManualExport)
        {
            Assert.Single(exportResults);
            Assert.Equal(ExportResult.Success, exportResults[0]);
        }
    }

    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Batch)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/logs", ExportProcessorType.Batch)]
    [InlineData(OtlpExportProtocol.Grpc, ":4317", ExportProcessorType.Simple)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":4318/v1/logs", ExportProcessorType.Simple)]
    [InlineData(OtlpExportProtocol.Grpc, ":5317", ExportProcessorType.Simple, "https")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":5318/v1/logs", ExportProcessorType.Simple, "https")]
    [InlineData(OtlpExportProtocol.Grpc, ":7317", ExportProcessorType.Batch, "https")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, ":7318/v1/traces", ExportProcessorType.Batch, "https")]
    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    public void TraceExportResultIsSuccess(
        OtlpExportProtocol protocol,
        string endpoint,
        ExportProcessorType exportProcessorType,
        string scheme = "http",
        bool useMtls = false)
    {
        using EventWaitHandle handle = new ManualResetEvent(false);

        var exporterOptions = new OtlpExporterOptions
        {
            Endpoint = new Uri($"{scheme}://{CollectorHostname}{endpoint}"),
            Protocol = protocol,
        };

        if (useMtls)
        {
            exporterOptions.CertificateFile = "/cfg/certs/otel-test-server-cert.pem";
            exporterOptions.ClientCertificateFile = "/cfg/certs/otel-test-client-cert.pem";
            exporterOptions.ClientKeyFile = "/cfg/certs/otel-test-client-key.pem";
        }

        DelegatingExporter<LogRecord> delegatingExporter;
        var exportResults = new List<ExportResult>();
        var processorOptions = new LogRecordExportProcessorOptions
        {
            ExportProcessorType = exportProcessorType,
            BatchExportProcessorOptions = new()
            {
                ScheduledDelayMilliseconds = ExportIntervalMilliseconds,
            },
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .UseOpenTelemetry(logging => logging
                    .AddProcessor(sp =>
                        OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                            sp,
                            exporterOptions,
                            processorOptions,
                            DefaultSdkLimitOptions,
                            DefaultExperimentalOptions,
                            configureExporterInstance: otlpExporter =>
                            {
                                delegatingExporter = new DelegatingExporter<LogRecord>
                                {
                                    OnExportFunc = (batch) =>
                                    {
                                        var result = otlpExporter.Export(batch);
                                        exportResults.Add(result);
                                        handle.Set();
                                        return result;
                                    },
                                };
                                return delegatingExporter;
                            })));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

        switch (processorOptions.ExportProcessorType)
        {
            case ExportProcessorType.Batch:
                Assert.True(handle.WaitOne(ExportIntervalMilliseconds * 2));
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
                break;
            case ExportProcessorType.Simple:
                Assert.Single(exportResults);
                Assert.Equal(ExportResult.Success, exportResults[0]);
                break;
            default:
                throw new NotSupportedException("Unexpected processor type encountered.");
        }
    }

    private sealed class OpenTelemetryEventListener : EventListener
    {
        private readonly ITestOutputHelper outputHelper;

        public OpenTelemetryEventListener(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);

            if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            string? message;
            if (eventData.Message != null && eventData.Payload != null && eventData.Payload.Count > 0)
            {
                message = string.Format(eventData.Message, eventData.Payload.ToArray());
            }
            else
            {
                message = eventData.Message;
            }

            this.outputHelper.WriteLine(message);
        }
    }
}
