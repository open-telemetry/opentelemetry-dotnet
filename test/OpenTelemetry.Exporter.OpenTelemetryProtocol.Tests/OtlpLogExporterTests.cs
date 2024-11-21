// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Proto.Common.V1.AnyValue;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpLogExporterTests
{
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();

    [Fact]
    public void AddOtlpExporterWithNamedOptions()
    {
        int defaultConfigureExporterOptionsInvocations = 0;
        int namedConfigureExporterOptionsInvocations = 0;

        int defaultConfigureSdkLimitsOptionsInvocations = 0;
        int namedConfigureSdkLimitsOptionsInvocations = 0;

        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<OtlpExporterOptions>(o => defaultConfigureExporterOptionsInvocations++);
                services.Configure<LogRecordExportProcessorOptions>(o => defaultConfigureExporterOptionsInvocations++);
                services.Configure<ExperimentalOptions>(o => defaultConfigureExporterOptionsInvocations++);

                services.Configure<OtlpExporterOptions>("Exporter2", o => namedConfigureExporterOptionsInvocations++);
                services.Configure<LogRecordExportProcessorOptions>("Exporter2", o => namedConfigureExporterOptionsInvocations++);
                services.Configure<ExperimentalOptions>("Exporter2", o => namedConfigureExporterOptionsInvocations++);

                services.Configure<OtlpExporterOptions>("Exporter3", o => namedConfigureExporterOptionsInvocations++);
                services.Configure<LogRecordExportProcessorOptions>("Exporter3", o => namedConfigureExporterOptionsInvocations++);
                services.Configure<ExperimentalOptions>("Exporter3", o => namedConfigureExporterOptionsInvocations++);

                services.Configure<SdkLimitOptions>(o => defaultConfigureSdkLimitsOptionsInvocations++);
                services.Configure<SdkLimitOptions>("Exporter2", o => namedConfigureSdkLimitsOptionsInvocations++);
                services.Configure<SdkLimitOptions>("Exporter3", o => namedConfigureSdkLimitsOptionsInvocations++);
            })
            .AddOtlpExporter()
            .AddOtlpExporter("Exporter2", o => { })
            .AddOtlpExporter("Exporter3", o => { })
            .Build();

        Assert.Equal(3, defaultConfigureExporterOptionsInvocations);
        Assert.Equal(6, namedConfigureExporterOptionsInvocations);

        // Note: SdkLimitOptions does NOT support named options. We only allow a
        // single instance for a given IServiceCollection.
        Assert.Equal(1, defaultConfigureSdkLimitsOptionsInvocations);
        Assert.Equal(0, namedConfigureSdkLimitsOptionsInvocations);
    }

    [Fact]
    public void UserHttpFactoryCalledWhenUsingHttpProtobuf()
    {
        OtlpExporterOptions options = new OtlpExporterOptions();

        var defaultFactory = options.HttpClientFactory;

        int invocations = 0;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.HttpClientFactory = () =>
        {
            invocations++;
            return defaultFactory();
        };

        using (var exporter = new OtlpLogExporter(options))
        {
            Assert.Equal(1, invocations);
        }

        using (var provider = Sdk.CreateLoggerProviderBuilder()
            .AddOtlpExporter(o =>
            {
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.HttpClientFactory = options.HttpClientFactory;
            })
            .Build())
        {
            Assert.Equal(2, invocations);
        }

        options.HttpClientFactory = () => null!;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var exporter = new OtlpLogExporter(options);
        });
    }

    [Fact]
    public void AddOtlpExporterSetsDefaultBatchExportProcessor()
    {
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddOtlpExporter()
            .Build();

        var loggerProviderSdk = loggerProvider as LoggerProviderSdk;
        Assert.NotNull(loggerProviderSdk);

        var batchProcessor = loggerProviderSdk.Processor as BatchLogRecordExportProcessor;
        Assert.NotNull(batchProcessor);

        Assert.Equal(BatchLogRecordExportProcessor.DefaultScheduledDelayMilliseconds, batchProcessor.ScheduledDelayMilliseconds);
        Assert.Equal(BatchLogRecordExportProcessor.DefaultExporterTimeoutMilliseconds, batchProcessor.ExporterTimeoutMilliseconds);
        Assert.Equal(BatchLogRecordExportProcessor.DefaultMaxExportBatchSize, batchProcessor.MaxExportBatchSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddOtlpLogExporterReceivesAttributesWithParseStateValueSetToFalse(bool callUseOpenTelemetry)
    {
        bool optionsValidated = false;

        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            ConfigureOtlpExporter(builder, callUseOpenTelemetry, logRecords: logRecords);

            builder.Services.Configure<OpenTelemetryLoggerOptions>(o =>
            {
                optionsValidated = true;
                Assert.False(o.ParseStateValues);
            });
        });

        Assert.True(optionsValidated);

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);
        var logRecord = logRecords[0];
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.NotNull(logRecord.State);
#pragma warning restore CS0618 // Type or member is obsolete
        Assert.NotNull(logRecord.Attributes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void AddOtlpLogExporterParseStateValueCanBeTurnedOff(bool parseState, bool callUseOpenTelemetry)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            ConfigureOtlpExporter(
                builder,
                callUseOpenTelemetry,
                configureOptions: o => o.ParseStateValues = parseState,
                logRecords: logRecords);
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.Log(LogLevel.Information, default, new { propertyA = "valueA" }, null, (s, e) => "Custom state log message");
        Assert.Single(logRecords);

        var logRecord = logRecords[0];

#pragma warning disable CS0618 // Type or member is obsolete
        if (parseState)
        {
            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.Attributes);

            // Note: We currently do not support parsing custom states which do
            // not implement the standard interfaces. We return empty attributes
            // for these.
            Assert.Empty(logRecord.Attributes);
        }
        else
        {
            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.Attributes);
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void AddOtlpLogExporterParseStateValueCanBeTurnedOffHosting(bool parseState, bool callUseOpenTelemetry)
    {
        var logRecords = new List<LogRecord>();

        var hostBuilder = new HostBuilder();
        hostBuilder.ConfigureLogging(logging =>
        {
            ConfigureOtlpExporter(logging, callUseOpenTelemetry, logRecords: logRecords);
        });

        hostBuilder.ConfigureServices(services =>
        services.Configure<OpenTelemetryLoggerOptions>(options => options.ParseStateValues = parseState));

        var host = hostBuilder.Build();
        var loggerFactory = host.Services.GetService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);
        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.Log(LogLevel.Information, default, new { propertyA = "valueA" }, null, (s, e) => "Custom state log message");
        Assert.Single(logRecords);

        var logRecord = logRecords[0];

#pragma warning disable CS0618 // Type or member is obsolete
        if (parseState)
        {
            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.Attributes);

            // Note: We currently do not support parsing custom states which do
            // not implement the standard interfaces. We return empty attributes
            // for these.
            Assert.Empty(logRecord.Attributes);
        }
        else
        {
            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.Attributes);
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OtlpLogRecordTestWhenStateValuesArePopulated(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;
                });
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

        Assert.Single(logRecords);

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
        Assert.Equal(3, otlpLogRecord.Attributes.Count);
        var index = 0;
        var attribute = otlpLogRecord.Attributes[index];

        Assert.Equal("name", attribute.Key);
        Assert.Equal("tomato", attribute.Value.StringValue);

        attribute = otlpLogRecord.Attributes[++index];
        Assert.Equal("price", attribute.Key);
        Assert.Equal(2.99, attribute.Value.DoubleValue);

        attribute = otlpLogRecord.Attributes[++index];
        Assert.Equal("{OriginalFormat}", attribute.Key);
        Assert.Equal("Hello from {name} {price}.", attribute.Value.StringValue);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData(null, true)]
    [InlineData("true", false)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void CheckToOtlpLogRecordEventId(string? emitLogEventAttributes, bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;
                });
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation(new EventId(10, null), "Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);

        var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string?> { [ExperimentalOptions.EmitLogEventEnvVar] = emitLogEventAttributes })
          .Build();

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new(configuration));

        var logRecord = logRecords[0];

        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new(configuration), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

        // Event
        var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
        if (emitLogEventAttributes == "true")
        {
            Assert.Contains(ExperimentalOptions.LogRecordEventIdAttribute, otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);
        }
        else
        {
            Assert.DoesNotContain(ExperimentalOptions.LogRecordEventIdAttribute, otlpLogRecordAttributes);
        }

        logRecords.Clear();

        logger.LogInformation(new EventId(10, "MyEvent10"), "Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);

        logRecord = logRecords[0];

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new(configuration), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

        // Event
        otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
        if (emitLogEventAttributes == "true")
        {
            Assert.Contains(ExperimentalOptions.LogRecordEventIdAttribute, otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);
            Assert.Contains(ExperimentalOptions.LogRecordEventNameAttribute, otlpLogRecordAttributes);
            Assert.Contains("MyEvent10", otlpLogRecordAttributes);
        }
        else
        {
            Assert.DoesNotContain(ExperimentalOptions.LogRecordEventIdAttribute, otlpLogRecordAttributes);
            Assert.DoesNotContain(ExperimentalOptions.LogRecordEventNameAttribute, otlpLogRecordAttributes);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckToOtlpLogRecordTimestamps(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging.AddInMemoryExporter(logRecords));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Log message");
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.True(otlpLogRecord.TimeUnixNano > 0);
        Assert.True(otlpLogRecord.ObservedTimeUnixNano > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckToOtlpLogRecordTraceIdSpanIdFlagWithNoActivity(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging.AddInMemoryExporter(logRecords));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Log when there is no activity.");

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.Null(Activity.Current);
        Assert.NotNull(otlpLogRecord);
        Assert.True(otlpLogRecord.TraceId.IsEmpty);
        Assert.True(otlpLogRecord.SpanId.IsEmpty);
        Assert.Equal(0u, otlpLogRecord.Flags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckToOtlpLogRecordSpanIdTraceIdAndFlag(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging.AddInMemoryExporter(logRecords));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");

        ActivityTraceId expectedTraceId = default;
        ActivitySpanId expectedSpanId = default;
        using (var activity = new Activity(Utils.GetCurrentMethodName()).Start())
        {
            logger.LogInformation("Log within an activity.");

            expectedTraceId = activity.TraceId;
            expectedSpanId = activity.SpanId;
        }

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];

        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.Equal(expectedTraceId.ToString(), ActivityTraceId.CreateFromBytes(otlpLogRecord.TraceId.ToByteArray()).ToString());
        Assert.Equal(expectedSpanId.ToString(), ActivitySpanId.CreateFromBytes(otlpLogRecord.SpanId.ToByteArray()).ToString());
        Assert.Equal((uint)logRecord.TraceFlags, otlpLogRecord.Flags);
    }

    [Theory]
    [InlineData(LogLevel.Trace, true)]
    [InlineData(LogLevel.Debug, true)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, false)]
    [InlineData(LogLevel.Critical, false)]
    public void CheckToOtlpLogRecordSeverityLevelAndText(LogLevel logLevel, bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .UseOpenTelemetry(
                    logging => logging.AddInMemoryExporter(logRecords),
                    options => options.IncludeFormattedMessage = true)
                .AddFilter("CheckToOtlpLogRecordSeverityLevelAndText", LogLevel.Trace);
        });

        var logger = loggerFactory.CreateLogger("CheckToOtlpLogRecordSeverityLevelAndText");
        logger.Log(logLevel, "Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Equal(logRecord.LogLevel.ToString(), otlpLogRecord.SeverityText);
#pragma warning restore CS0618 // Type or member is obsolete
        Assert.NotNull(logRecord.Severity);
        Assert.Equal((int)logRecord.Severity, (int)otlpLogRecord.SeverityNumber);
        switch (logLevel)
        {
            case LogLevel.Trace:
                Assert.Equal(OtlpLogs.SeverityNumber.Trace, otlpLogRecord.SeverityNumber);
                break;
            case LogLevel.Debug:
                Assert.Equal(OtlpLogs.SeverityNumber.Debug, otlpLogRecord.SeverityNumber);
                break;
            case LogLevel.Information:
                Assert.Equal(OtlpLogs.SeverityNumber.Info, otlpLogRecord.SeverityNumber);
                break;
            case LogLevel.Warning:
                Assert.Equal(OtlpLogs.SeverityNumber.Warn, otlpLogRecord.SeverityNumber);
                break;
            case LogLevel.Error:
                Assert.Equal(OtlpLogs.SeverityNumber.Error, otlpLogRecord.SeverityNumber);
                break;
            case LogLevel.Critical:
                Assert.Equal(OtlpLogs.SeverityNumber.Fatal, otlpLogRecord.SeverityNumber);
                break;
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void CheckToOtlpLogRecordBodyIsPopulated(bool includeFormattedMessage, bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options =>
                {
                    options.IncludeFormattedMessage = includeFormattedMessage;
                    options.ParseStateValues = true;
                });
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");

        // Scenario 1 - Using ExtensionMethods on ILogger.Log
        logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
        Assert.Single(logRecords);

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        if (includeFormattedMessage)
        {
            Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
        }
        else
        {
            Assert.Equal("OpenTelemetry {Greeting} {Subject}!", otlpLogRecord.Body.StringValue);
        }

        logRecords.Clear();

        // Scenario 2 - Using the raw ILogger.Log Method
        logger.Log(LogLevel.Information, default, "state", exception: null, (st, ex) => "Formatted Message");
        Assert.Single(logRecords);

        logRecord = logRecords[0];

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);

        // Formatter is always called if no template can be found.
        Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
        Assert.Equal(logRecord.Body, otlpLogRecord.Body.StringValue);

        logRecords.Clear();

        // Scenario 3 - Using the raw ILogger.Log Method, but with null
        // formatter.
        logger.Log(LogLevel.Information, default, "state", exception: null, formatter: null!);
        Assert.Single(logRecords);

        logRecord = logRecords[0];

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);

        // There is no formatter, we call ToString on state
        Assert.Equal("state", otlpLogRecord.Body.StringValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LogRecordBodyIsExportedWhenUsingBridgeApi(bool isBodySet)
    {
        LogRecordAttributeList attributes = default;
        attributes.Add("name", "tomato");
        attributes.Add("price", 2.99);
        attributes.Add("{OriginalFormat}", "Hello from {name} {price}.");

        var logRecords = new List<LogRecord>();

        using (var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(logRecords)
            .Build())
        {
            var logger = loggerProvider.GetLogger();

            logger.EmitLog(new LogRecordData()
            {
                Body = isBodySet ? "Hello world" : null,
            });

            logger.EmitLog(new LogRecordData(), attributes);
        }

        Assert.Equal(2, logRecords.Count);

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecords[0]);

        Assert.NotNull(otlpLogRecord);
        if (isBodySet)
        {
            Assert.Equal("Hello world", otlpLogRecord.Body?.StringValue);
        }
        else
        {
            Assert.Null(otlpLogRecord.Body);
        }

        otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecords[1]);

        Assert.NotNull(otlpLogRecord);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);

        var index = 0;
        var attribute = otlpLogRecord.Attributes[index];
        Assert.Equal("name", attribute.Key);
        Assert.Equal("tomato", attribute.Value.StringValue);

        attribute = otlpLogRecord.Attributes[++index];
        Assert.Equal("price", attribute.Key);
        Assert.Equal(2.99, attribute.Value.DoubleValue);

        Assert.Equal("Hello from {name} {price}.", otlpLogRecord.Body.StringValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckToOtlpLogRecordExceptionAttributes(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging.AddInMemoryExporter(logRecords));
        });

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation(new Exception("Exception Message"), "Exception Occurred");

        var logRecord = logRecords[0];
        var loggedException = logRecord.Exception;

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();

        Assert.Contains(SemanticConventions.AttributeExceptionType, otlpLogRecordAttributes);
        Assert.NotNull(logRecord.Exception);
        Assert.Contains(logRecord.Exception.GetType().Name, otlpLogRecordAttributes);

        Assert.Contains(SemanticConventions.AttributeExceptionMessage, otlpLogRecordAttributes);
        Assert.Contains(logRecord.Exception.Message, otlpLogRecordAttributes);

        Assert.Contains(SemanticConventions.AttributeExceptionStacktrace, otlpLogRecordAttributes);
        Assert.Contains(logRecord.Exception.ToInvariantString(), otlpLogRecordAttributes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckToOtlpLogRecordRespectsAttributeLimits(bool useCustomSerializer)
    {
        var sdkLimitOptions = new SdkLimitOptions
        {
            AttributeCountLimit = 2,
            AttributeValueLengthLimit = 8,
        };

        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.ParseStateValues = true);
        });

        var logger = loggerFactory.CreateLogger(string.Empty);
        logger.LogInformation("OpenTelemetry {AttributeOne} {AttributeTwo} {AttributeThree}!", "I'm an attribute", "I too am an attribute", "I get dropped :(");

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(sdkLimitOptions, new());

        var logRecord = logRecords[0];
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(sdkLimitOptions, new(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        Assert.Equal(1u, otlpLogRecord.DroppedAttributesCount);

        var attribute = TryGetAttribute(otlpLogRecord, "AttributeOne");
        Assert.NotNull(attribute);

        // "I'm an a" == first 8 chars from the first attribute "I'm an attribute"
        Assert.Equal("I'm an a", attribute.Value.StringValue);
        attribute = TryGetAttribute(otlpLogRecord, "AttributeTwo");
        Assert.NotNull(attribute);

        // "I too am" == first 8 chars from the second attribute "I too am an attribute"
        Assert.Equal("I too am", attribute.Value.StringValue);

        attribute = TryGetAttribute(otlpLogRecord, "AttributeThree");
        Assert.Null(attribute);
    }

    [Fact]
    public void Export_WhenExportClientIsProvidedInCtor_UsesProvidedExportClient()
    {
        // Arrange.
        var testExportClient = new TestExportClient<OtlpCollector.ExportLogsServiceRequest>();
        var exporterOptions = new OtlpExporterOptions();
        var transmissionHandler = new OtlpExporterTransmissionHandler<OtlpCollector.ExportLogsServiceRequest>(testExportClient, exporterOptions.TimeoutMilliseconds);
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        var sut = new OtlpLogExporter(
            exporterOptions,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            transmissionHandler);

        // Act.
        sut.Export(emptyBatch);

        // Assert.
        Assert.True(testExportClient.SendExportRequestCalled);
    }

    [Fact]
    public void Export_WhenExportClientThrowsException_ReturnsExportResultFailure()
    {
        // Arrange.
        var testExportClient = new TestExportClient<OtlpCollector.ExportLogsServiceRequest>(throwException: true);
        var exporterOptions = new OtlpExporterOptions();
        var transmissionHandler = new OtlpExporterTransmissionHandler<OtlpCollector.ExportLogsServiceRequest>(testExportClient, exporterOptions.TimeoutMilliseconds);
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        var sut = new OtlpLogExporter(
            exporterOptions,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            transmissionHandler);

        // Act.
        var result = sut.Export(emptyBatch);

        // Assert.
        Assert.Equal(ExportResult.Failure, result);
    }

    [Fact]
    public void Export_WhenExportIsSuccessful_ReturnsExportResultSuccess()
    {
        // Arrange.
        var testExportClient = new TestExportClient<OtlpCollector.ExportLogsServiceRequest>();
        var exporterOptions = new OtlpExporterOptions();
        var transmissionHandler = new OtlpExporterTransmissionHandler<OtlpCollector.ExportLogsServiceRequest>(testExportClient, exporterOptions.TimeoutMilliseconds);
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        var sut = new OtlpLogExporter(
            exporterOptions,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            transmissionHandler);

        // Act.
        var result = sut.Export(emptyBatch);

        // Assert.
        Assert.Equal(ExportResult.Success, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsFalse_DoesNotContainScopeAttribute(bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = false);
        });
        var logger = loggerFactory.CreateLogger("Some category");

        const string expectedScopeKey = "Some scope key";
        const string expectedScopeValue = "Some scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(expectedScopeKey, expectedScopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        OtlpLogs.LogRecord? otlpLogRecord;

        if (useCustomSerializer)
        {
            otlpLogRecord = ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord);
        }
        else
        {
            otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        }

        Assert.NotNull(otlpLogRecord);
        var actualScope = TryGetAttribute(otlpLogRecord, expectedScopeKey);
        Assert.Null(actualScope);
    }

    [Theory]
    [InlineData("Some scope value", false)]
    [InlineData('a', false)]
    [InlineData("Some scope value", true)]
    [InlineData('a', true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeStringValue(object scopeValue, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.StringValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeBoolValue(bool scopeValue, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.BoolValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.BoolValue.ToString());
    }

    [Theory]
    [InlineData(byte.MinValue, false)]
    [InlineData(byte.MaxValue, false)]
    [InlineData(sbyte.MinValue, false)]
    [InlineData(sbyte.MaxValue, false)]
    [InlineData(short.MinValue, false)]
    [InlineData(short.MaxValue, false)]
    [InlineData(ushort.MinValue, false)]
    [InlineData(ushort.MaxValue, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MaxValue, false)]
    [InlineData(uint.MinValue, false)]
    [InlineData(uint.MaxValue, false)]
    [InlineData(long.MinValue, false)]
    [InlineData(long.MaxValue, false)]
    [InlineData(byte.MinValue, true)]
    [InlineData(byte.MaxValue, true)]
    [InlineData(sbyte.MinValue, true)]
    [InlineData(sbyte.MaxValue, true)]
    [InlineData(short.MinValue, true)]
    [InlineData(short.MaxValue, true)]
    [InlineData(ushort.MinValue, true)]
    [InlineData(ushort.MaxValue, true)]
    [InlineData(int.MinValue, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(uint.MinValue, true)]
    [InlineData(uint.MaxValue, true)]
    [InlineData(long.MinValue, true)]
    [InlineData(long.MaxValue, true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeIntValue(object scopeValue, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.IntValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.IntValue.ToString());
    }

    [Theory]
    [InlineData(float.MinValue, false)]
    [InlineData(float.MaxValue, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(float.MaxValue, true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeDoubleValueForFloat(float scopeValue, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.DoubleValue, actualScope.Value.ValueCase);
        Assert.Equal(((double)scopeValue).ToString(), actualScope.Value.DoubleValue.ToString());
    }

    [Theory]
    [InlineData(double.MinValue, false)]
    [InlineData(double.MaxValue, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(double.MaxValue, true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeDoubleValueForDouble(double scopeValue, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.DoubleValue.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfTypeString_ScopeIsIgnored(bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeState = "Some scope state";

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Empty(otlpLogRecord.Attributes);
    }

    [Theory]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(float), false)]
    [InlineData(typeof(decimal), false)]
    [InlineData(typeof(char), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(float), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(char), true)]
    [InlineData(typeof(bool), true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfPrimitiveTypes_ScopeIsIgnored(Type typeOfScopeState, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        var scopeState = Activator.CreateInstance(typeOfScopeState);
        Assert.NotNull(scopeState);

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Empty(otlpLogRecord.Attributes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfDictionaryType_ScopeIsProcessed(bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";
        const string scopeValue = "Some scope value";
        var scopeState = new Dictionary<string, object>() { { scopeKey, scopeValue } };

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue, actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData(typeof(List<KeyValuePair<string, object>>), false)]
    [InlineData(typeof(ReadOnlyCollection<KeyValuePair<string, object>>), false)]
    [InlineData(typeof(HashSet<KeyValuePair<string, object>>), false)]
    [InlineData(typeof(List<KeyValuePair<string, object>>), true)]
    [InlineData(typeof(ReadOnlyCollection<KeyValuePair<string, object>>), true)]
    [InlineData(typeof(HashSet<KeyValuePair<string, object>>), true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfEnumerableType_ScopeIsProcessed(Type typeOfScopeState, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";
        const string scopeValue = "Some scope value";
        var scopeValues = new List<KeyValuePair<string, object?>> { new(scopeKey, scopeValue) };
        var scopeState = Activator.CreateInstance(typeOfScopeState, scopeValues) as ICollection<KeyValuePair<string, object>>;

        // Act.
        Assert.NotNull(scopeState);
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue, actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key", false)]
    [InlineData("Scope key 1", "Scope key 2", false)]
    [InlineData("Same scope key", "Same scope key", true)]
    [InlineData("Scope key 1", "Scope key 2", true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndMultipleScopesAreAdded_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey1, scopeValue1),
            new KeyValuePair<string, object>(scopeKey2, scopeValue2),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key", false)]
    [InlineData("Scope key 1", "Scope key 2", false)]
    [InlineData("Same scope key", "Same scope key", true)]
    [InlineData("Scope key 1", "Scope key 2", true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndMultipleScopeLevelsAreAdded_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey1, scopeValue1) }))
        {
            using (logger.BeginScope(new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey2, scopeValue2) }))
            {
                logger.LogInformation("Some log information message.");
            }
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key", false)]
    [InlineData("Scope key 1", "Scope key 2", false)]
    [InlineData("Same scope key", "Same scope key", true)]
    [InlineData("Scope key 1", "Scope key 2", true)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeIsUsedInLogMethod_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2, bool useCustomSerializer)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(
                logging => logging.AddInMemoryExporter(logRecords),
                options => options.IncludeScopes = true);
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey1, scopeValue1),
        }))
        {
            logger.Log(
                LogLevel.Error,
                new EventId(1),
                new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey2, scopeValue2) },
                exception: new Exception("Some exception message"),
                formatter: (s, e) => string.Empty);
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var otlpLogRecord = useCustomSerializer
            ? ToOtlpLogs(DefaultSdkLimitOptions, new ExperimentalOptions(), logRecord)
            : otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(5, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddOtlpLogExporterDefaultOptionsTest(bool callUseOpenTelemetry)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            ConfigureOtlpExporter(builder, callUseOpenTelemetry);
        });

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ILoggerFactory>();

        var provider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;
        Assert.NotNull(provider);

        var batchProcesor = provider.Processor as BatchLogRecordExportProcessor;
        Assert.NotNull(batchProcesor);

        Assert.Equal(BatchLogRecordExportProcessor.DefaultScheduledDelayMilliseconds, batchProcesor.ScheduledDelayMilliseconds);
    }

    [Theory]
    [InlineData(ExportProcessorType.Simple, false)]
    [InlineData(ExportProcessorType.Batch, false)]
    [InlineData(ExportProcessorType.Simple, true)]
    [InlineData(ExportProcessorType.Batch, true)]
    public void AddOtlpLogExporterLogRecordProcessorOptionsTest(ExportProcessorType processorType, bool callUseOpenTelemetry)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            ConfigureOtlpExporter(
                builder,
                callUseOpenTelemetry,
                configureExporterAndProcessor: (e, p) =>
                {
                    p.ExportProcessorType = processorType;
                    p.BatchExportProcessorOptions = new BatchExportLogRecordProcessorOptions() { ScheduledDelayMilliseconds = 1000 };
                });
        });

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ILoggerFactory>();

        var provider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;
        Assert.NotNull(provider);

        var processor = provider.Processor;
        Assert.NotNull(processor);

        if (processorType == ExportProcessorType.Batch)
        {
            var batchProcesor = processor as BatchLogRecordExportProcessor;
            Assert.NotNull(batchProcesor);

            Assert.Equal(1000, batchProcesor.ScheduledDelayMilliseconds);
        }
        else
        {
            var simpleProcessor = processor as SimpleLogRecordExportProcessor;

            Assert.NotNull(simpleProcessor);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateInstrumentationScope(bool useCustomSerializer)
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging.AddInMemoryExporter(logRecords));
        });

        var logger1 = loggerFactory.CreateLogger("OtlpLogExporterTests-A");
        logger1.LogInformation("Hello from red-tomato");

        var logger2 = loggerFactory.CreateLogger("OtlpLogExporterTests-B");
        logger2.LogInformation("Hello from green-tomato");

        Assert.Equal(2, logRecords.Count);

        var batch = new Batch<LogRecord>(logRecords.ToArray(), logRecords.Count);
        var logRecordTransformer = new OtlpLogRecordTransformer(new(), new());

        var resourceBuilder = ResourceBuilder.CreateEmpty();
        var processResource = resourceBuilder.Build().ToOtlpResource();

        OtlpCollector.ExportLogsServiceRequest request;
        if (useCustomSerializer)
        {
            request = CreateLogsExportRequest(DefaultSdkLimitOptions, new ExperimentalOptions(), batch, resourceBuilder.Build());
        }
        else
        {
            request = logRecordTransformer.BuildExportRequest(processResource, batch);
        }

        Assert.Single(request.ResourceLogs);

        var scope1 = request.ResourceLogs[0].ScopeLogs.First();
        var scope2 = request.ResourceLogs[0].ScopeLogs.Last();

        Assert.Equal("OtlpLogExporterTests-A", scope1.Scope.Name);
        Assert.Equal("OtlpLogExporterTests-B", scope2.Scope.Name);

        Assert.Single(scope1.LogRecords);
        Assert.Single(scope2.LogRecords);

        var logrecord1 = scope1.LogRecords[0];
        var logrecord2 = scope2.LogRecords[0];

        Assert.Equal("Hello from red-tomato", logrecord1.Body.StringValue);

        Assert.Equal("Hello from green-tomato", logrecord2.Body.StringValue);

        // Validate LogListPool
        Assert.Empty(OtlpLogRecordTransformer.LogListPool);
        logRecordTransformer.Return(request);
        Assert.Equal(2, OtlpLogRecordTransformer.LogListPool.Count);

        if (useCustomSerializer)
        {
            request = CreateLogsExportRequest(DefaultSdkLimitOptions, new ExperimentalOptions(), batch, resourceBuilder.Build());
        }
        else
        {
            request = logRecordTransformer.BuildExportRequest(processResource, batch);

            // ScopeLogs will be reused.
            Assert.Empty(OtlpLogRecordTransformer.LogListPool);
        }

        Assert.Single(request.ResourceLogs);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("logging", false)]
    [InlineData(null, true)]
    [InlineData("logging", true)]
    public void VerifyEnvironmentVariablesTakenFromIConfigurationWhenUsingLoggerFactoryCreate(string? optionsName, bool callUseOpenTelemetry)
    {
        RunVerifyEnvironmentVariablesTakenFromIConfigurationTest(
            optionsName,
            configure =>
            {
                var factory = LoggerFactory.Create(logging =>
                {
                    configure(logging.Services);

                    ConfigureOtlpExporter(
                        logging,
                        callUseOpenTelemetry,
                        name: optionsName);
                });

                return (factory, factory);
            });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("logging", false)]
    [InlineData(null, true)]
    [InlineData("logging", true)]
    public void VerifyEnvironmentVariablesTakenFromIConfigurationWhenUsingLoggingBuilder(string? optionsName, bool callUseOpenTelemetry)
    {
        RunVerifyEnvironmentVariablesTakenFromIConfigurationTest(
            optionsName,
            configure =>
            {
                var services = new ServiceCollection();

                configure(services);

                services.AddLogging(
                    logging => ConfigureOtlpExporter(
                        logging,
                        callUseOpenTelemetry,
                        name: optionsName));

                var sp = services.BuildServiceProvider();

                var factory = sp.GetRequiredService<ILoggerFactory>();

                return (sp, factory);
            });
    }

    [Theory]
    [InlineData("my_instrumentation_scope_name", "my_instrumentation_scope_name", true)]
    [InlineData(null, "", true)]
    [InlineData("my_instrumentation_scope_name", "my_instrumentation_scope_name", false)]
    [InlineData(null, "", false)]
    public void LogRecordLoggerNameIsExportedWhenUsingBridgeApi(string? loggerName, string expectedScopeName, bool useCustomSerializer)
    {
        LogRecordAttributeList attributes = default;
        attributes.Add("name", "tomato");
        attributes.Add("price", 2.99);
        attributes.Add("{OriginalFormat}", "Hello from {name} {price}.");

        var logRecords = new List<LogRecord>();

        using (var loggerProvider = Sdk.CreateLoggerProviderBuilder()
                   .AddInMemoryExporter(logRecords)
                   .Build())
        {
            var logger = loggerProvider.GetLogger(loggerName);

            logger.EmitLog(new LogRecordData());
        }

        Assert.Single(logRecords);

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var batch = new Batch<LogRecord>(new[] { logRecords[0] }, 1);

        OtlpCollector.ExportLogsServiceRequest request;
        if (useCustomSerializer)
        {
            request = CreateLogsExportRequest(DefaultSdkLimitOptions, new ExperimentalOptions(), batch, ResourceBuilder.CreateEmpty().Build());
        }
        else
        {
            request = otlpLogRecordTransformer.BuildExportRequest(
            new Proto.Resource.V1.Resource(),
            batch);
        }

        Assert.NotNull(request);
        Assert.Single(request.ResourceLogs);
        Assert.Single(request.ResourceLogs[0].ScopeLogs);

        Assert.Equal(expectedScopeName, request.ResourceLogs[0].ScopeLogs[0].Scope?.Name);
    }

    private static void RunVerifyEnvironmentVariablesTakenFromIConfigurationTest(
        string? optionsName,
        Func<Action<IServiceCollection>, (IDisposable Container, ILoggerFactory LoggerFactory)> createLoggerFactoryFunc)
    {
        var values = new Dictionary<string, string?>
        {
            [OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName] = "http://test:8888",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var configureDelegateCalled = false;
        var configureExportProcessorOptionsCalled = false;
        var configureBatchOptionsCalled = false;

        var tracingConfigureDelegateCalled = false;
        var unnamedConfigureDelegateCalled = false;
        var allConfigureDelegateCalled = false;

        var testState = createLoggerFactoryFunc(services =>
        {
            services.AddSingleton<IConfiguration>(configuration);

            services.Configure<OtlpExporterOptions>(optionsName, o =>
            {
                configureDelegateCalled = true;
                Assert.Equal(new Uri("http://test:8888"), o.Endpoint);
            });

            services.Configure<LogRecordExportProcessorOptions>(optionsName, o =>
            {
                configureExportProcessorOptionsCalled = true;
            });

            services.Configure<BatchExportLogRecordProcessorOptions>(optionsName, o =>
            {
                configureBatchOptionsCalled = true;
            });

            services.Configure<OtlpExporterOptions>("tracing", o =>
            {
                tracingConfigureDelegateCalled = true;
            });

            services.Configure<OtlpExporterOptions>(o =>
            {
                unnamedConfigureDelegateCalled = true;
            });

            services.ConfigureAll<OtlpExporterOptions>(o =>
            {
                allConfigureDelegateCalled = true;
            });
        });

        using var container = testState.Container;

        var factory = testState.LoggerFactory;

        Assert.NotNull(factory);

        Assert.True(configureDelegateCalled);
        Assert.True(configureExportProcessorOptionsCalled);
        Assert.True(configureBatchOptionsCalled);

        Assert.False(tracingConfigureDelegateCalled);

        Assert.Equal(optionsName == null, unnamedConfigureDelegateCalled);

        Assert.True(allConfigureDelegateCalled);
    }

    private static OtlpCommon.KeyValue? TryGetAttribute(OtlpLogs.LogRecord record, string key)
    {
        return record.Attributes.FirstOrDefault(att => att.Key == key);
    }

    private static void ConfigureOtlpExporter(
        ILoggingBuilder builder,
        bool callUseOpenTelemetry,
        string? name = null,
        Action<OtlpExporterOptions>? configureExporter = null,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions>? configureExporterAndProcessor = null,
        Action<OpenTelemetryLoggerOptions>? configureOptions = null,
        List<LogRecord>? logRecords = null)
    {
        if (callUseOpenTelemetry)
        {
            builder.UseOpenTelemetry(
                logging =>
                {
                    if (configureExporterAndProcessor != null)
                    {
                        logging.AddOtlpExporter(name, configureExporterAndProcessor);
                    }
                    else
                    {
                        logging.AddOtlpExporter(name, configureExporter);
                    }

                    if (logRecords != null)
                    {
                        logging.AddInMemoryExporter(logRecords);
                    }
                },
                options =>
                {
                    configureOptions?.Invoke(options);
                });
        }
        else
        {
#pragma warning disable CS0618 // Type or member is obsolete
            builder.AddOpenTelemetry(options =>
            {
                configureOptions?.Invoke(options);

                if (configureExporterAndProcessor != null)
                {
                    options.AddOtlpExporter(name, configureExporterAndProcessor);
                }
                else
                {
                    options.AddOtlpExporter(name, configureExporter);
                }

                if (logRecords != null)
                {
                    options.AddInMemoryExporter(logRecords);
                }
            });
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    private static OtlpCollector.ExportLogsServiceRequest CreateLogsExportRequest(SdkLimitOptions sdkOptions, ExperimentalOptions experimentalOptions, in Batch<LogRecord> batch, Resource resource)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpLogSerializer.WriteLogsData(buffer, 0, sdkOptions, experimentalOptions, resource, batch);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var logsData = OtlpLogs.LogsData.Parser.ParseFrom(stream);
        var request = new OtlpCollector.ExportLogsServiceRequest();
        request.ResourceLogs.Add(logsData.ResourceLogs);
        return request;
    }

    private static OtlpLogs.LogRecord? ToOtlpLogs(SdkLimitOptions sdkOptions, ExperimentalOptions experimentalOptions, LogRecord logRecord)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpLogSerializer.WriteLogRecord(buffer, 0, sdkOptions, experimentalOptions, logRecord);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeLogs = OtlpLogs.ScopeLogs.Parser.ParseFrom(stream);
        return scopeLogs.LogRecords.FirstOrDefault();
    }
}
