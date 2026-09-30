// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpHttpClientLoggingTests
{
    private const string MeterName = "OtlpHttpClientLoggingTests";
    private const string OtlpLogExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpLogExporter";
    private const string OtlpMetricExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpMetricExporter";
    private const string OtlpTraceExporterHttpClientCategory = "System.Net.Http.HttpClient.OtlpTraceExporter";

    [Fact]
    public void SuccessfulMetricExportDoesNotEmitInformationLogs()
    {
        using var httpMessageHandler = new TestHttpMessageHandler();
        using var loggerProvider = new CollectingLoggerProvider();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(loggerProvider);
        });
        services
            .AddHttpClient("OtlpMetricExporter")
            .ConfigurePrimaryHttpMessageHandler(() => httpMessageHandler);

        services.AddOpenTelemetry().WithMetrics(builder => builder
            .AddMeter(MeterName)
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri("http://localhost:4318");
            }));

        using var serviceProvider = services.BuildServiceProvider();
        using var meter = new Meter(MeterName);

        meter.CreateCounter<long>("requests").Add(1);

        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();
        Assert.True(meterProvider.ForceFlush());
        Assert.NotNull(httpMessageHandler.HttpRequestMessage);

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Category.StartsWith(OtlpMetricExporterHttpClientCategory, StringComparison.Ordinal));
    }

    [Fact]
    public void FailedMetricExportReportsExporterError()
    {
        const int FailedToReachCollectorEventId = 2;

        using var httpMessageHandler = new ThrowingHttpMessageHandler();
        using var loggerProvider = new CollectingLoggerProvider();
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(loggerProvider);
        });
        services
            .AddHttpClient("OtlpMetricExporter")
            .ConfigurePrimaryHttpMessageHandler(() => httpMessageHandler);

        services.AddOpenTelemetry().WithMetrics(builder => builder
            .AddMeter(MeterName)
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri("http://localhost:4318");
            }));

        using var serviceProvider = services.BuildServiceProvider();
        using var meter = new Meter(MeterName);

        meter.CreateCounter<long>("requests").Add(1);

        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();
        Assert.False(meterProvider.ForceFlush());
        Assert.True(httpMessageHandler.RequestAttempted);

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry => entry.LogLevel == LogLevel.Information
                && entry.Category.StartsWith(OtlpMetricExporterHttpClientCategory, StringComparison.Ordinal));
        Assert.Single(listener.Messages, entry => entry.EventId == FailedToReachCollectorEventId);
    }

    [Theory]
    [InlineData(OtlpLogExporterHttpClientCategory)]
    [InlineData(OtlpMetricExporterHttpClientCategory)]
    [InlineData(OtlpTraceExporterHttpClientCategory)]
    public void OtlpHttpClientCategoriesSuppressInformationButPreserveWarnings(string categoryName)
    {
        using var loggerProvider = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        AddExporterServices(services, categoryName);

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"{categoryName}.LogicalHandler");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitApplicationFilterCanEnableInformationLogs(bool configureBeforeExporter)
    {
        using var loggerProvider = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        if (configureBeforeExporter)
        {
            AddApplicationFilter(services);
        }

        services.AddOtlpExporterMetricsServices(Options.DefaultName);

        if (!configureBeforeExporter)
        {
            AddApplicationFilter(services);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger($"{OtlpMetricExporterHttpClientCategory}.LogicalHandler");

        Assert.True(logger.IsEnabled(LogLevel.Information));

        static void AddApplicationFilter(IServiceCollection services)
            => services.Configure<LoggerFilterOptions>(options => options.Rules.Add(new LoggerFilterRule(
                providerName: null,
                OtlpMetricExporterHttpClientCategory,
                LogLevel.Information,
                filter: null)));
    }

    [Fact]
    public void OtlpHttpClientFiltersAreRegisteredOnce()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOtlpExporterLoggingServices();
        services.AddOtlpExporterMetricsServices(Options.DefaultName);
        services.AddOtlpExporterMetricsServices("Exporter2");
        services.AddOtlpExporterTracingServices();

        using var serviceProvider = services.BuildServiceProvider();
        var loggerFilterOptions = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

        Assert.Single(loggerFilterOptions.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
        Assert.Single(loggerFilterOptions.Rules, rule => rule.CategoryName == OtlpMetricExporterHttpClientCategory);
        Assert.Single(loggerFilterOptions.Rules, rule => rule.CategoryName == OtlpTraceExporterHttpClientCategory);
    }

    private static void AddExporterServices(IServiceCollection services, string categoryName)
    {
        switch (categoryName)
        {
            case OtlpLogExporterHttpClientCategory:
                services.AddOtlpExporterLoggingServices();
                break;
            case OtlpMetricExporterHttpClientCategory:
                services.AddOtlpExporterMetricsServices(Options.DefaultName);
                break;
            case OtlpTraceExporterHttpClientCategory:
                services.AddOtlpExporterTracingServices();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(categoryName));
        }
    }

    private sealed class LogEntry(string category, LogLevel logLevel, EventId eventId)
    {
        public string Category { get; } = category;

        public LogLevel LogLevel { get; } = logLevel;

        public EventId EventId { get; } = eventId;
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, this.Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new(category, logLevel, eventId));
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        public bool RequestAttempted { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestAttempted = true;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("Test exception."));
        }

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestAttempted = true;
            throw new HttpRequestException("Test exception.");
        }
#endif
    }
}
