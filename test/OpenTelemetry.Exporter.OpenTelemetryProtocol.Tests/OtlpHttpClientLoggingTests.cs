// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void LogExportUsingOpenTelemetryLoggerOptionsSuppressesInformationLogsAfterReload(
        bool configureExporterAndProcessor,
        bool namedOptions,
        bool requestLoggerProviderFirst)
    {
        using var httpMessageHandler = new TestHttpMessageHandler();
        using var loggerProvider = new CollectingLoggerProvider();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().Build();

        var configureInvocations = 0;
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration);
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(loggerProvider);
            builder.AddOpenTelemetry(options =>
            {
                configureInvocations++;
                if (configureExporterAndProcessor)
                {
                    if (namedOptions)
                    {
                        options.AddOtlpExporter("NamedExporter", (exporter, processor) => ConfigureExporter(exporter));
                    }
                    else
                    {
                        options.AddOtlpExporter((exporter, processor) => ConfigureExporter(exporter));
                    }
                }
                else if (namedOptions)
                {
                    options.AddOtlpExporter("NamedExporter", ConfigureExporter);
                }
                else
                {
                    options.AddOtlpExporter(ConfigureExporter);
                }
            });
        });
        services.AddHttpClient("OtlpLogExporter")
            .ConfigurePrimaryHttpMessageHandler(() => httpMessageHandler);

        using var serviceProvider = services.BuildServiceProvider();
        if (requestLoggerProviderFirst)
        {
            serviceProvider.GetRequiredService<LoggerProvider>();
        }

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(OtlpHttpClientLoggingTests));

        logger.HelloFrom("tomato", 2.99);
        Assert.NotNull(httpMessageHandler.HttpRequestMessage);
        Assert.DoesNotContain(loggerProvider.Entries, IsHttpClientInformationLog);

        configuration.Reload();
        logger.HelloFrom("tomato", 2.99);

        Assert.DoesNotContain(loggerProvider.Entries, IsHttpClientInformationLog);
        Assert.Equal(1, configureInvocations);

        var httpClientLogger = loggerFactory.CreateLogger($"{OtlpLogExporterHttpClientCategory}.LogicalHandler");
        Assert.False(httpClientLogger.IsEnabled(LogLevel.Information));
        Assert.True(httpClientLogger.IsEnabled(LogLevel.Warning));
        Assert.True(httpClientLogger.IsEnabled(LogLevel.Error));
        Assert.Single(
            serviceProvider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>().CurrentValue.Rules,
            rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);

        static bool IsHttpClientInformationLog(LogEntry entry)
            => entry.LogLevel == LogLevel.Information
                && entry.Category.StartsWith(OtlpLogExporterHttpClientCategory, StringComparison.Ordinal);

        static void ConfigureExporter(OtlpExporterOptions options)
        {
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Endpoint = new Uri("http://localhost:4318");
            options.ExportProcessorType = ExportProcessorType.Simple;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenTelemetryLoggerOptionsPreservesApplicationFilterAfterReload(bool configureBeforeExporter)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration);
            if (configureBeforeExporter)
            {
                builder.AddFilter(OtlpLogExporterHttpClientCategory, LogLevel.Information);
            }

            builder.AddOpenTelemetry(options => options.AddOtlpExporter());

            if (!configureBeforeExporter)
            {
                builder.AddFilter(OtlpLogExporterHttpClientCategory, LogLevel.Information);
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger($"{OtlpLogExporterHttpClientCategory}.LogicalHandler");

        Assert.True(logger.IsEnabled(LogLevel.Information));
        configuration.Reload();
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenTelemetryLoggerOptionsRegistersDefaultFilterOnce(bool registerExporterServices)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddOtlpExporter());
            builder.AddOpenTelemetry(options => options.AddOtlpExporter());
        });
        if (registerExporterServices)
        {
            services.AddOtlpExporterLoggingServices();
        }

        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<ILoggerFactory>();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>().CurrentValue;

        var rule = Assert.Single(options.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
        Assert.Equal(LogLevel.Warning, rule.LogLevel);
    }

    [Fact]
    public void PostConfiguredExporterRebuildsEarlyMonitorOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddOpenTelemetry());
        services.PostConfigure<OpenTelemetryLoggerOptions>(options => options.AddOtlpExporter());

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var options = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
        var monitorOptions = serviceProvider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>().CurrentValue;
        var snapshotOptions = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<LoggerFilterOptions>>().Value;

        Assert.NotSame(options, monitorOptions);
        Assert.NotSame(options, snapshotOptions);
        Assert.NotSame(monitorOptions, snapshotOptions);

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger($"{OtlpLogExporterHttpClientCategory}.LogicalHandler");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        foreach (var filters in new[] { options, monitorOptions, snapshotOptions })
        {
            Assert.DoesNotContain(filters.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
        }

        var currentOptions = serviceProvider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>().CurrentValue;
        Assert.NotSame(monitorOptions, currentOptions);
        var rule = Assert.Single(currentOptions.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
        Assert.Equal(LogLevel.Warning, rule.LogLevel);
    }

    [Fact]
    public async Task ExporterRegistrationDoesNotModifyFilterOptionsBeingConfigured()
    {
        using var filterConfigurationStarted = new ManualResetEventSlim();
        using var completeFilterConfiguration = new ManualResetEventSlim();
        var invocations = 0;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddOpenTelemetry(options => options.AddOtlpExporter()));
        services.Configure<LoggerFilterOptions>(options =>
        {
            if (Interlocked.Increment(ref invocations) == 1)
            {
                filterConfigurationStarted.Set();
                Assert.True(completeFilterConfiguration.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
                Assert.DoesNotContain(options.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>();
        var pendingOptions = Task.Run(() => monitor.CurrentValue, TestContext.Current.CancellationToken);
        try
        {
            Assert.True(filterConfigurationStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger($"{OtlpLogExporterHttpClientCategory}.LogicalHandler");

            Assert.False(logger.IsEnabled(LogLevel.Information));
        }
        finally
        {
            completeFilterConfiguration.Set();
        }

        Assert.Same(
            pendingOptions,
            await Task.WhenAny(pendingOptions, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)));
        var earlyOptions = await pendingOptions;
        Assert.NotSame(earlyOptions, monitor.CurrentValue);
        Assert.DoesNotContain(earlyOptions.Rules, rule => rule.CategoryName == OtlpLogExporterHttpClientCategory);
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
        {
            services.Configure<LoggerFilterOptions>(options => options.Rules.Add(new LoggerFilterRule(
                providerName: null,
                OtlpMetricExporterHttpClientCategory,
                LogLevel.Information,
                filter: null)));
        }
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
