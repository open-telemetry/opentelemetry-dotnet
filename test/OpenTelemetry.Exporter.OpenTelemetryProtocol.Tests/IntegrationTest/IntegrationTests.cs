// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class IntegrationTests : IDisposable
{
    private const string CollectorHostnameEnvVarName = "OTEL_COLLECTOR_HOSTNAME";
    private const int ExportIntervalMilliseconds = 10_000;
    private const string GrpcEndpointHttp = ":4317";
    private const string GrpcEndpointHttps = ":5317";
    private const string ProtobufEndpointHttp = ":4318/v1/";
    private const string ProtobufEndpointHttps = ":5318/v1/";

    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();
    private static readonly ExperimentalOptions DefaultExperimentalOptions = new();
    private static readonly string? CollectorHostname = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(CollectorHostnameEnvVarName);

    private static readonly bool[] BooleanValues = [false, true];
    private static readonly ExportProcessorType[] ExportProcessorTypes = [ExportProcessorType.Batch, ExportProcessorType.Simple];

    private readonly OpenTelemetryEventListener openTelemetryEventListener;

    public IntegrationTests(ITestOutputHelper outputHelper)
    {
        this.openTelemetryEventListener = new(outputHelper);
    }

    public static TheoryData<OtlpExportProtocol, string, ExportProcessorType, bool, string> TraceTestCases()
    {
        var data = new TheoryData<OtlpExportProtocol, string, ExportProcessorType, bool, string>();

#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        foreach (var exportType in ExportProcessorTypes)
        {
            foreach (var forceFlush in BooleanValues)
            {
                data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttp, exportType, forceFlush, Uri.UriSchemeHttp);
                data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttp}traces", exportType, forceFlush, Uri.UriSchemeHttp);
            }
        }

        data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttps, ExportProcessorType.Simple, true, Uri.UriSchemeHttps);
        data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttps}traces", ExportProcessorType.Simple, true, Uri.UriSchemeHttps);
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        return data;
    }

    public static TheoryData<OtlpExportProtocol, string, bool, bool, string> MetricsTestCases()
    {
        var data = new TheoryData<OtlpExportProtocol, string, bool, bool, string>();

#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        foreach (var useManualExport in BooleanValues)
        {
            foreach (var forceFlush in BooleanValues)
            {
                data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttp, useManualExport, forceFlush, Uri.UriSchemeHttp);
                data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttp}metrics", useManualExport, forceFlush, Uri.UriSchemeHttp);
            }
        }

        data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttps, true, true, Uri.UriSchemeHttps);
        data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttps}metrics", true, true, Uri.UriSchemeHttps);
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        return data;
    }

    public static TheoryData<OtlpExportProtocol, string, ExportProcessorType, string> LogsTestCases()
    {
        var data = new TheoryData<OtlpExportProtocol, string, ExportProcessorType, string>();

#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        foreach (var exportType in ExportProcessorTypes)
        {
            data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttp, exportType, Uri.UriSchemeHttp);
            data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttp}logs", exportType, Uri.UriSchemeHttp);
        }

        data.Add(OtlpExportProtocol.Grpc, GrpcEndpointHttps, ExportProcessorType.Simple, Uri.UriSchemeHttps);
        data.Add(OtlpExportProtocol.HttpProtobuf, $"{ProtobufEndpointHttps}logs", ExportProcessorType.Simple, Uri.UriSchemeHttps);
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        return data;
    }

    public void Dispose() => this.openTelemetryEventListener.Dispose();

    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    [MemberData(nameof(TraceTestCases))]
    public void TraceExportResultIsSuccess(
        OtlpExportProtocol protocol,
        string endpoint,
        ExportProcessorType exportProcessorType,
        bool forceFlush,
        string scheme)
    {
        using var exported = new ManualResetEvent(false);

        var exporterOptions = CreateExporterOptions(protocol, scheme, endpoint);

        exporterOptions.ExportProcessorType = exportProcessorType;
        exporterOptions.BatchExportProcessorOptions = new()
        {
            ScheduledDelayMilliseconds = ExportIntervalMilliseconds,
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
                        exported.Set();
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
                AssertExpectedTraces();
            }
            else if (exporterOptions.ExportProcessorType == ExportProcessorType.Batch)
            {
                Assert.True(exported.WaitOne(ExportIntervalMilliseconds * 2));
                AssertExpectedTraces();
            }
        }

        if (!forceFlush && exportProcessorType == ExportProcessorType.Simple)
        {
            AssertExpectedTraces();
        }

        Assert.Empty(this.openTelemetryEventListener.Errors);
        Assert.Empty(this.openTelemetryEventListener.Warnings);

        void AssertExpectedTraces()
        {
            var result = Assert.Single(exportResults);
            Assert.Equal(ExportResult.Success, result);
        }
    }

    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    [MemberData(nameof(MetricsTestCases))]
    public void MetricExportResultIsSuccess(
        OtlpExportProtocol protocol,
        string endpoint,
        bool useManualExport,
        bool forceFlush,
        string scheme)
    {
        using var exported = new ManualResetEvent(false);

        var exporterOptions = CreateExporterOptions(protocol, scheme, endpoint);

        DelegatingExporter<Metric>? delegatingExporter = null;
        var exportResults = new List<ExportResult>();

        var meterName = "otlp.collector.test";

        var builder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddMeter("System.Net.Http", "System.Net.NameResolution", "System.Runtime");

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
                        exported.Set();
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

            var gauge = meter.CreateGauge<int>("test_gauge");
            gauge.Record(42);

            var histogram = meter.CreateHistogram<int>("test_histogram");
            histogram.Record(100);

            Assert.NotNull(delegatingExporter);

            if (forceFlush)
            {
                Assert.True(meterProvider.ForceFlush());
                AssertExpectedMetrics();
            }
            else if (!useManualExport)
            {
                Assert.True(exported.WaitOne(ExportIntervalMilliseconds * 2));
                AssertExpectedMetrics();
            }
        }

        if (!forceFlush && useManualExport)
        {
            AssertExpectedMetrics();
        }

        Assert.Empty(this.openTelemetryEventListener.Errors);
        Assert.Empty(this.openTelemetryEventListener.Warnings);

        void AssertExpectedMetrics()
        {
            var result = Assert.Single(exportResults);
            Assert.Equal(ExportResult.Success, result);
        }
    }

    [Trait("CategoryName", "CollectorIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(CollectorHostnameEnvVarName)]
    [MemberData(nameof(LogsTestCases))]
    public void LogExportResultIsSuccess(
        OtlpExportProtocol protocol,
        string endpoint,
        ExportProcessorType exportProcessorType,
        string scheme)
    {
        using var exported = new ManualResetEvent(false);

        var exporterOptions = CreateExporterOptions(protocol, scheme, endpoint);

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
                                        exported.Set();
                                        return result;
                                    },
                                };
                                return delegatingExporter;
                            })));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.HelloFrom("tomato", 2.99);

        switch (processorOptions.ExportProcessorType)
        {
            case ExportProcessorType.Batch:
                Assert.True(exported.WaitOne(ExportIntervalMilliseconds * 2));
                break;

            case ExportProcessorType.Simple:
                break;

            default:
                throw new NotSupportedException("Unexpected processor type encountered.");
        }

        var result = Assert.Single(exportResults);
        Assert.Equal(ExportResult.Success, result);

        Assert.Empty(this.openTelemetryEventListener.Errors);
        Assert.Empty(this.openTelemetryEventListener.Warnings);
    }

    private static OtlpExporterOptions CreateExporterOptions(OtlpExportProtocol protocol, string scheme, string endpoint) =>
        new()
        {
            Endpoint = new($"{scheme}://{CollectorHostname}{endpoint}"),
            Protocol = protocol,
        };

    private sealed class OpenTelemetryEventListener(ITestOutputHelper outputHelper) : EventListener
    {
        public List<string> Errors { get; } = [];

        public List<string> Warnings { get; } = [];

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
            var message = eventData.Message != null && eventData.Payload?.Count > 0
                ? string.Format(CultureInfo.InvariantCulture, eventData.Message, [.. eventData.Payload])
                : eventData.Message;

            message = string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", eventData.Level, message);

            outputHelper.WriteLine(message);

            if (eventData.Level == EventLevel.Error)
            {
                this.Errors.Add(message);
            }
            else if (eventData.Level == EventLevel.Warning)
            {
                this.Warnings.Add(message);
            }
        }
    }
}
