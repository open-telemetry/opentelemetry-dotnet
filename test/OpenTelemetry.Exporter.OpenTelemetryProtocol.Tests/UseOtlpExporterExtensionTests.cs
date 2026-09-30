// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("EnvVars")]
public sealed class UseOtlpExporterExtensionTests : IDisposable
{
    public UseOtlpExporterExtensionTests()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
    }

    public void Dispose()
        => OtlpSpecConfigDefinitionTests.ClearEnvVars();

    [Fact]
    public void UseOtlpExporterDefaultTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().CurrentValue;

#if NETFRAMEWORK
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), exporterOptions.DefaultOptions.Endpoint);
#else
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), exporterOptions.DefaultOptions.Endpoint);
#endif

        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, exporterOptions.DefaultOptions.Protocol);
        Assert.False(((OtlpExporterOptions)exporterOptions.DefaultOptions).HasData);

        Assert.False(((OtlpExporterOptions)exporterOptions.LoggingOptions).HasData);
        Assert.False(((OtlpExporterOptions)exporterOptions.MetricsOptions).HasData);
        Assert.False(((OtlpExporterOptions)exporterOptions.TracingOptions).HasData);
    }

    [Fact]
    public void UseOtlpExporterRespectsConfiguredOtlpExporterOptionsTest()
    {
        var services = new ServiceCollection();
        var configuredEndpoint = new Uri("http://configured_endpoint/");

        services.Configure<OtlpExporterOptions>(options => options.Endpoint = configuredEndpoint);
        services.AddOpenTelemetry()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<TracerProvider>());

        var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().CurrentValue;

        Assert.Equal(configuredEndpoint, exporterOptions.DefaultOptions.Endpoint);
        Assert.Equal(configuredEndpoint, exporterOptions.LoggingOptions.Endpoint);
        Assert.Equal(configuredEndpoint, exporterOptions.MetricsOptions.Endpoint);
        Assert.Equal(configuredEndpoint, exporterOptions.TracingOptions.Endpoint);
    }

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc)]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.HttpProtobuf)]
    public void UseOtlpExporterSetEndpointAndProtocolTest(OtlpExportProtocol protocol)
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter(
                protocol,
                new Uri("http://test_base_endpoint/"));

        using var sp = services.BuildServiceProvider();

        var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().CurrentValue;

        Assert.Equal(protocol, exporterOptions.DefaultOptions.Protocol);
        Assert.Equal(new Uri("http://test_base_endpoint/"), exporterOptions.DefaultOptions.Endpoint);
        Assert.True(((OtlpExporterOptions)exporterOptions.DefaultOptions).HasData);

        Assert.False(((OtlpExporterOptions)exporterOptions.LoggingOptions).HasData);
        Assert.False(((OtlpExporterOptions)exporterOptions.MetricsOptions).HasData);
        Assert.False(((OtlpExporterOptions)exporterOptions.TracingOptions).HasData);

        Assert.Throws<ArgumentNullException>(
            () => services.AddOpenTelemetry().UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, baseUrl: null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("testNamedOptions")]
    public void UseOtlpExporterConfigureTest(string? name)
    {
        var services = new ServiceCollection();

        if (!string.IsNullOrEmpty(name))
        {
            services.AddOpenTelemetry()
                .UseOtlpExporter(name, configuration: null, configure: Configure);
        }
        else
        {
            services.AddOpenTelemetry()
                .UseOtlpExporter(Configure);
        }

        using var sp = services.BuildServiceProvider();

        VerifyOptionsApplied(sp, name);

        static void Configure(OtlpExporterBuilder builder)
        {
            builder.ConfigureDefaultExporterOptions(
                defaultOptions => defaultOptions.Endpoint = new Uri("http://default_endpoint/"));

            builder.ConfigureLoggingExporterOptions(
                exporterOptions => exporterOptions.Endpoint = new Uri("http://signal_endpoint/logs/"));
            builder.ConfigureLoggingProcessorOptions(
                processorOptions =>
                {
                    processorOptions.ExportProcessorType = ExportProcessorType.Simple;
                    processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1000;
                });

            builder.ConfigureMetricsExporterOptions(
                exporterOptions => exporterOptions.Endpoint = new Uri("http://signal_endpoint/metrics/"));
            builder.ConfigureMetricsReaderOptions(
                readerOptions =>
                {
                    readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1001;
                });

            builder.ConfigureTracingExporterOptions(
                exporterOptions => exporterOptions.Endpoint = new Uri("http://signal_endpoint/traces/"));
            builder.ConfigureTracingProcessorOptions(
                processorOptions =>
                {
                    processorOptions.ExportProcessorType = ExportProcessorType.Simple;
                    processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1002;
                });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("testNamedOptions")]
    public void UseOtlpExporterConfigurationTest(string? name)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DefaultOptions:Endpoint"] = "http://default_endpoint/",
                ["LoggingOptions:Endpoint"] = "http://signal_endpoint/logs/",
                ["LoggingOptions:ExportProcessorType"] = "Simple",
                ["LoggingOptions:BatchExportProcessorOptions:ScheduledDelayMilliseconds"] = "1000",
                ["MetricsOptions:Endpoint"] = "http://signal_endpoint/metrics/",
                ["MetricsOptions:TemporalityPreference"] = "Delta",
                ["MetricsOptions:PeriodicExportingMetricReaderOptions:ExportIntervalMilliseconds"] = "1001",
                ["TracingOptions:Endpoint"] = "http://signal_endpoint/traces/",
                ["TracingOptions:ExportProcessorType"] = "Simple",
                ["TracingOptions:BatchExportProcessorOptions:ScheduledDelayMilliseconds"] = "1002",
            })
            .Build();

        var services = new ServiceCollection();

        if (!string.IsNullOrEmpty(name))
        {
            services.AddOpenTelemetry()
                .UseOtlpExporter(name: name, configuration: config, configure: null);
        }
        else
        {
            services.AddOpenTelemetry()
                .UseOtlpExporter(config);

            name = "otlp";
        }

        using var sp = services.BuildServiceProvider();

        VerifyOptionsApplied(sp, name);
    }

    [Fact]
    public void UseOtlpExporterSingleCallsTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
    }

    [Fact]
    public void UseOtlpExporterMultipleCallsTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(sp.GetRequiredService<LoggerProvider>);
        Assert.Throws<NotSupportedException>(sp.GetRequiredService<MeterProvider>);
        Assert.Throws<NotSupportedException>(sp.GetRequiredService<TracerProvider>);
    }

    [Fact]
    public void UseOtlpExporterWithAddOtlpExporterLoggingTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithLogging(builder => builder.AddOtlpExporter());

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(sp.GetRequiredService<LoggerProvider>);
    }

    [Fact]
    public void UseOtlpExporterWithAddOtlpExporterMetricsTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithMetrics(builder => builder.AddOtlpExporter());

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(sp.GetRequiredService<MeterProvider>);
    }

    [Fact]
    public void UseOtlpExporterWithAddOtlpExporterTracingTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithTracing(builder => builder.AddOtlpExporter());

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(sp.GetRequiredService<TracerProvider>);
    }

    [Fact]
    public void UseOtlpExporterAddsTracingProcessorToPipelineEndTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithTracing(builder => builder.AddProcessor(new TestActivityProcessor()));

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<TracerProvider>() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);

        var processor = tracerProvider.Processor as CompositeProcessor<Activity>;

        Assert.NotNull(processor);

        var processors = processor.ToReadOnlyList();

        Assert.True(processors[0] is TestActivityProcessor);
        Assert.True(processors[1] is BatchActivityExportProcessor);
    }

    [Fact]
    public void UseOtlpExporterAddsLoggingProcessorToPipelineEndTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithLogging(builder => builder.AddProcessor(new TestLogRecordProcessor()));

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(tracerProvider);

        var processor = tracerProvider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        var processors = processor.ToReadOnlyList();

        Assert.True(processors[0] is TestLogRecordProcessor);
        Assert.True(processors[1] is BatchLogRecordExportProcessor);
    }

    [Fact]
    public void UseOtlpExporterRespectsSpecEnvVarsTest()
    {
        OtlpSpecConfigDefinitionTests.SetEnvVars();

        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        var exporterBuilderOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(Options.DefaultName);

        OtlpSpecConfigDefinitionTests.DefaultData.AssertMatches(exporterBuilderOptions.DefaultOptions);
        OtlpSpecConfigDefinitionTests.LoggingData.AssertMatches(exporterBuilderOptions.LoggingOptions);
        OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches(exporterBuilderOptions.MetricsOptions);
        OtlpSpecConfigDefinitionTests.TracingData.AssertMatches(exporterBuilderOptions.TracingOptions);

        var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(Options.DefaultName);

        OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches(metricReaderOptions);
    }

    [Fact]
    public void UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest()
    {
        var services = new ServiceCollection();

        services.AddSingleton(OtlpSpecConfigDefinitionTests.ToConfiguration());

        services.AddOpenTelemetry()
            .UseOtlpExporter();

        using var sp = services.BuildServiceProvider();

        var exporterBuilderOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(Options.DefaultName);

        OtlpSpecConfigDefinitionTests.DefaultData.AssertMatches(exporterBuilderOptions.DefaultOptions);
        OtlpSpecConfigDefinitionTests.LoggingData.AssertMatches(exporterBuilderOptions.LoggingOptions);
        OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches(exporterBuilderOptions.MetricsOptions);
        OtlpSpecConfigDefinitionTests.TracingData.AssertMatches(exporterBuilderOptions.TracingOptions);

        var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(Options.DefaultName);

        OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches(metricReaderOptions);
    }

    [Fact]
    public void UseOtlpExporterAppliesConfiguredAttributeAndSpanLimits()
    {
        using var requestCapture = new OtlpHttpRequestCapture();
        using var activitySource = new ActivitySource(
            new ActivitySourceOptions(nameof(this.UseOtlpExporterAppliesConfiguredAttributeAndSpanLimits))
            {
                Tags = [new("1_ScopeTag", "a"), new("2_ScopeTag", "b"), new("3_ScopeTag", "c")],
            });

        var services = new ServiceCollection();
        services.Configure<AttributeLimitOptions>(options => options.AttributeCountLimit = 2);
        services.Configure<SpanLimitOptions>(options => options.AttributeCountLimit = 1);
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(activitySource.Name))
            .UseOtlpExporter(builder =>
            {
                builder.ConfigureDefaultExporterOptions(requestCapture.ConfigureTransport);
                builder.ConfigureTracingProcessorOptions(options => options.ExportProcessorType = ExportProcessorType.Simple);
            });

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<TracerProvider>());

        using (var activity = activitySource.StartActivity(
            "Test",
            ActivityKind.Internal,
            default(ActivityContext),
            new ActivityTagsCollection
            {
                { "First", "one" },
                { "Second", "two" },
            }))
        {
            Assert.NotNull(activity);
        }

        var scopeSpans = requestCapture.GetSingleScopeSpans();
        Assert.NotNull(scopeSpans.Scope);
        Assert.Equal(2, scopeSpans.Scope.Attributes.Count);
        Assert.Equal(1u, scopeSpans.Scope.DroppedAttributesCount);

        var span = Assert.Single(scopeSpans.Spans);
        Assert.Single(span.Attributes);
        Assert.Equal(1u, span.DroppedAttributesCount);
    }

    [Fact]
    public void UseOtlpExporterAppliesConfiguredLogRecordLimits()
    {
        using var requestCapture = new OtlpHttpRequestCapture();

        var services = new ServiceCollection();
        services.Configure<LogRecordLimitOptions>(options => options.AttributeCountLimit = 1);
        services.AddOpenTelemetry()
            .UseOtlpExporter(builder =>
            {
                builder.ConfigureDefaultExporterOptions(requestCapture.ConfigureTransport);
                builder.ConfigureLoggingProcessorOptions(options => options.ExportProcessorType = ExportProcessorType.Simple);
            });

        using var sp = services.BuildServiceProvider();

        LogRecordAttributeList attributes = default;
        attributes.Add("First", "one");
        attributes.Add("Second", "two");
        sp.GetRequiredService<LoggerProvider>()
            .GetLogger(nameof(this.UseOtlpExporterAppliesConfiguredLogRecordLimits))
            .EmitLog(new LogRecordData(), attributes);

        var logRecord = requestCapture.GetSingleLogRecord();
        Assert.Single(logRecord.Attributes);
        Assert.Equal(1u, logRecord.DroppedAttributesCount);
    }

    private static void VerifyOptionsApplied(ServiceProvider serviceProvider, string? name)
    {
        var exporterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(name);

        Assert.Equal("http://default_endpoint/", exporterOptions.DefaultOptions.Endpoint.ToString());
        /* Note: False is OK here. For cross-cutting extension
           AppendSignalPathToEndpoint on default options isn't used for anything */
        Assert.False(((OtlpExporterOptions)exporterOptions.DefaultOptions).AppendSignalPathToEndpoint);

        Assert.Equal("http://signal_endpoint/logs/", exporterOptions.LoggingOptions.Endpoint.ToString());
        Assert.False(((OtlpExporterOptions)exporterOptions.LoggingOptions).AppendSignalPathToEndpoint);

        Assert.Equal("http://signal_endpoint/metrics/", exporterOptions.MetricsOptions.Endpoint.ToString());
        Assert.False(((OtlpExporterOptions)exporterOptions.MetricsOptions).AppendSignalPathToEndpoint);

        Assert.Equal("http://signal_endpoint/traces/", exporterOptions.TracingOptions.Endpoint.ToString());
        Assert.False(((OtlpExporterOptions)exporterOptions.TracingOptions).AppendSignalPathToEndpoint);

        var logRecordProcessorOptions = serviceProvider.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(name);

        Assert.Equal(ExportProcessorType.Simple, logRecordProcessorOptions.ExportProcessorType);
        Assert.Equal(1000, logRecordProcessorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds);

        var metricReaderOptions = serviceProvider.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

        Assert.Equal(MetricReaderTemporalityPreference.Delta, metricReaderOptions.TemporalityPreference);
        Assert.Equal(1001, metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds);

        var activityProcessorOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ActivityExportProcessorOptions>>().Get(name);

        Assert.Equal(ExportProcessorType.Simple, activityProcessorOptions.ExportProcessorType);
        Assert.Equal(1002, activityProcessorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds);
    }

    private sealed class TestLogRecordProcessor : BaseProcessor<LogRecord>;
}
