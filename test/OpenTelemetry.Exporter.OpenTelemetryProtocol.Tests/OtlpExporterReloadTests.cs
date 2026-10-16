// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpExporterReloadTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SharedNamedRegistrationsPreserveHttpClientOwnership(bool callerOwned, bool allSignals)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        using var handler = new RecordingHandler(new ConcurrentQueue<(Uri Uri, string Header)>());
        var factory = new TrackingHttpClientFactory(handler);
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(factory);
        services.Configure<OtlpExporterOptions>("shared", configuration);
        if (callerOwned)
        {
            services.Configure<OtlpExporterOptions>("shared", options => options.HttpClientFactory = () => factory.CreateClient("caller"));
        }

        services.AddLogging();
        var builder = services.AddOpenTelemetry().WithTracing(tracing => tracing.AddOtlpExporter("shared", configure: null));
        if (allSignals)
        {
            builder.WithMetrics(metrics => metrics.AddOtlpExporter("shared", configure: null))
                .WithLogging(logging => logging.AddOtlpExporter("shared", configureExporter: null));
        }
        else
        {
            builder.WithTracing(tracing => tracing.AddOtlpExporter("shared", configure: null));
        }

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<TracerProvider>();
        try
        {
            LoggerProvider? logs = null;
            Action<ILogger, Exception?>? log = null;
            ILogger? logger = null;
            if (allSignals)
            {
                serviceProvider.GetRequiredService<MeterProvider>();
                logs = serviceProvider.GetRequiredService<LoggerProvider>();
                logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ownership-test");
                log = LoggerMessage.Define(LogLevel.Information, new EventId(1), "test");
                log(logger, null);
                Assert.True(logs.ForceFlush());
            }

            configuration.Reload();
            configuration.Reload();
            if (log != null)
            {
                log(logger!, null);
                Assert.True(logs!.ForceFlush());
            }

            serviceProvider.Dispose();
            Assert.Equal(allSignals ? 9 : 6, factory.Clients.Count);
            Assert.All(factory.Clients, client => Assert.Equal(callerOwned ? 0 : 1, client.DisposeCount));
            if (callerOwned || !allSignals)
            {
                Assert.All(factory.Names, name => Assert.Equal(callerOwned ? "caller" : "OtlpTraceExporter", name));
            }
            else
            {
                foreach (var name in new[] { "OtlpTraceExporter", "OtlpMetricExporter", "OtlpLogExporter" })
                {
                    Assert.Equal(3, factory.Names.Count(actual => actual == name));
                }
            }
        }
        finally
        {
            foreach (var client in factory.Clients)
            {
                client.Dispose();
            }
        }
    }

    [Fact]
    public void SharedNamedSignalsKeepInlineCallbacksAndPipelineOptionsPrivate()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Protocol"] = "HttpProtobuf",
            ["Headers"] = "test=common",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var metricCallbacks = new List<MetricReaderOptions>();
        var logCallbacks = new List<LogRecordExportProcessorOptions>();
        var headers = new ConcurrentQueue<(string Path, string[] Names)>();
        using var handler = new RecordingHandler(
            requests,
            request => headers.Enqueue((request.RequestUri!.AbsolutePath, request.Headers.Select(header => header.Key).ToArray())));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>("shared", configuration);
        services.Configure<OtlpExporterOptions>("shared", options => options.HttpClientFactory = () => httpClient);
        services.AddLogging();
        services.AddOpenTelemetry()
            .WithTracing(builder => builder.AddSource("shared-test").AddOtlpExporter("shared", options =>
            {
                options.Headers += ",trace=common";
                options.Endpoint = new Uri("http://localhost:4318/traces");
            }))
            .WithMetrics(builder => builder.AddMeter("shared-test").AddOtlpExporter("shared", (options, reader) =>
            {
                options.Headers += ",metric=local";
                options.Endpoint = new Uri("http://localhost:4318/metrics");
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 60000 + metricCallbacks.Count;
                metricCallbacks.Add(reader);
            }))
            .WithLogging(builder => builder.AddOtlpExporter("shared", (options, processor) =>
            {
                options.Headers += ",log=local";
                options.Endpoint = new Uri("http://localhost:4318/logs");
                processor.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 60000 + logCallbacks.Count;
                logCallbacks.Add(processor);
            }));
        using var serviceProvider = services.BuildServiceProvider();
        var traces = serviceProvider.GetRequiredService<TracerProvider>();
        var metrics = serviceProvider.GetRequiredService<MeterProvider>();
        var logs = serviceProvider.GetRequiredService<LoggerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("shared-test"));
        using var meter = new Meter(new MeterOptions("shared-test"));
        var counter = meter.CreateCounter<int>("requests");
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("shared-test");
        var log = LoggerMessage.Define(LogLevel.Information, new EventId(1), "test");
        for (var generation = 0; generation < 3; generation++)
        {
            if (generation > 0)
            {
                configuration.Reload();
            }

            using (source.StartActivity("test"))
            {
            }

            counter.Add(1);
            log(logger, null);
            Assert.True(traces.ForceFlush());
            Assert.True(metrics.ForceFlush());
            Assert.True(logs.ForceFlush());
            var cached = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("shared");
            Assert.Equal("test=common,trace=common", cached.Headers);
        }

        Assert.Equal(3, metricCallbacks.Count);
        Assert.Equal(3, logCallbacks.Count);
        Assert.Equal(60000, metricCallbacks[0].PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds);
        Assert.Equal(60000, logCallbacks[0].BatchExportProcessorOptions.ScheduledDelayMilliseconds);
        Assert.NotSame(metricCallbacks[0], metricCallbacks[1]);
        Assert.NotSame(logCallbacks[0], logCallbacks[1]);
        Assert.NotSame(metricCallbacks[0].PeriodicExportingMetricReaderOptions, metricCallbacks[1].PeriodicExportingMetricReaderOptions);
        Assert.NotSame(logCallbacks[0].BatchExportProcessorOptions, logCallbacks[1].BatchExportProcessorOptions);
        Assert.All(requests, request => Assert.Equal("common", request.Header));
        Assert.Equal(9, headers.Count);
        Assert.All(headers, header =>
        {
            Assert.Contains("trace", header.Names);
            Assert.Equal(header.Path.EndsWith("/metrics", StringComparison.Ordinal), header.Names.Contains("metric"));
            Assert.Equal(header.Path.EndsWith("/logs", StringComparison.Ordinal), header.Names.Contains("log"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTraceExporterReloadsEndpointAndHeadersWithoutRecreatingProvider(bool changeProtocol)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
            ["Headers"] = "test=first",
            ["TimeoutMilliseconds"] = "1000",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var clients = new List<HttpClient>();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpTraceExporter")
            .ConfigureHttpClient(clients.Add)
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));
        services.Configure<OtlpExporterOptions>("trace", configuration);
        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddSource("reload-test")
            .AddOtlpExporter("trace", configure: null));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("reload-test"));

        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first" && r.Header == "first");
        Assert.Equal(TimeSpan.FromMilliseconds(1000), clients.Last().Timeout);

        if (changeProtocol)
        {
            configuration["Protocol"] = "Grpc";
        }

        configuration["Endpoint"] = "http://localhost:4318/second";
        configuration["Headers"] = "test=second";
        configuration["TimeoutMilliseconds"] = "2000";
        configuration.Reload();
        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second" && r.Header == "second");
        Assert.Equal(TimeSpan.FromMilliseconds(2000), clients.Last().Timeout);
        Assert.Equal(
            changeProtocol ? "Grpc" : "HttpProtobuf",
            serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("trace").Protocol.ToString());

        configuration["Endpoint"] = "http://localhost:4318/invalid";
        configuration["Headers"] = "invalid";
        configuration.Reload();
        Export();
        Assert.Equal("/second", requests.Last().Uri.AbsolutePath);

        configuration["Endpoint"] = "http://localhost:4318/third";
        configuration["Headers"] = "test=third";
        configuration.Reload();
        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/third");
        Assert.Same(provider, serviceProvider.GetRequiredService<TracerProvider>());

        provider.Dispose();
        configuration["Endpoint"] = "http://localhost:4318/after-dispose";
        configuration.Reload();

        void Export()
        {
            using (source.StartActivity("test"))
            {
            }

            Assert.True(provider.ForceFlush());
        }
    }

    [Fact]
    public async Task ReloadDoesNotCancelAnExportAlreadyUsingThePreviousClient()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpTraceExporter").ConfigurePrimaryHttpMessageHandler(() => new BlockingHandler(requests, entered, release));
        services.Configure<OtlpExporterOptions>("trace", configuration);
        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddSource("reload-race-test")
            .AddOtlpExporter("trace", configure: null));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("reload-race-test"));
        var firstExport = Task.Run(() =>
        {
            using (source.StartActivity("first"))
            {
            }

            return provider.ForceFlush();
        });

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            configuration["Endpoint"] = "http://localhost:4318/second";
            var reload = Task.Run(configuration.Reload, TestContext.Current.CancellationToken);
            Assert.Same(reload, await Task.WhenAny(reload, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).ConfigureAwait(true));
            await reload.ConfigureAwait(true);
        }
        finally
        {
            release.Set();
        }

        Assert.True(await firstExport.ConfigureAwait(true));
        using (source.StartActivity("second"))
        {
        }

        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first");
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second");
    }

    [Fact]
    public void NamedMetricExporterReloadsEndpointWithoutRecreatingProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpMetricExporter").ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));
        services.Configure<OtlpExporterOptions>("metric", configuration);
        services.AddOpenTelemetry().WithMetrics(builder => builder
            .AddMeter("reload-test")
            .AddOtlpExporter("metric", (exporter, _) => exporter.Headers = "test=combined"));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<MeterProvider>();
        using var meter = new Meter(new MeterOptions("reload-test"));
        var counter = meter.CreateCounter<int>("requests");

        counter.Add(1);
        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first");

        configuration["Endpoint"] = "http://localhost:4318/second";
        configuration.Reload();
        counter.Add(1);
        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second" && r.Header == "combined");
        Assert.Same(provider, serviceProvider.GetRequiredService<MeterProvider>());
    }

    [Fact]
    public void NamedLogExporterReloadsEndpointWithoutRecreatingProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpLogExporter").ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));
        services.Configure<OtlpExporterOptions>("log", configuration);
        services.AddLogging();
        services.AddOpenTelemetry().WithLogging(builder => builder.AddOtlpExporter("log", configureExporter: null));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<LoggerProvider>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("reload-test");
        var log = LoggerMessage.Define(LogLevel.Information, new EventId(1, "Reload"), "test");

        log(logger, null);
        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first");

        configuration["Endpoint"] = "http://localhost:4318/second";
        configuration.Reload();
        log(logger, null);
        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second");
        Assert.Same(provider, serviceProvider.GetRequiredService<LoggerProvider>());
    }

    [Fact]
    public void UseOtlpExporterReloadsEndpointWithoutRecreatingProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultOptions:Endpoint"] = "http://localhost:4318/first",
            ["DefaultOptions:Protocol"] = "HttpProtobuf",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpTraceExporter").ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));
        services.AddOpenTelemetry()
            .UseOtlpExporter("otlp", configuration, configure: null)
            .WithTracing(builder => builder.AddSource("reload-test"));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("reload-test"));

        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first/v1/traces");

        configuration["DefaultOptions:Endpoint"] = "http://localhost:4318/second";
        configuration.Reload();
        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second/v1/traces");
        Assert.Same(provider, serviceProvider.GetRequiredService<TracerProvider>());

        void Export()
        {
            using (source.StartActivity("test"))
            {
            }

            Assert.True(provider.ForceFlush());
        }
    }

    [Fact]
    public void UseOtlpExporterReloadsDefaultExporterOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>(configuration);
        services.Configure<OtlpExporterOptions>(options => options.HttpClientFactory = () => new HttpClient(new RecordingHandler(requests)));
        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .WithTracing(builder => builder.AddSource("reload-default-test"));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("reload-default-test"));

        using (var activity = source.StartActivity("first"))
        {
            Assert.NotNull(activity);
        }

        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first/v1/traces");

        configuration["Endpoint"] = "http://localhost:4318/second";
        configuration.Reload();
        using (source.StartActivity("second"))
        {
        }

        Assert.True(provider.ForceFlush());
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second/v1/traces");
    }

    [Fact]
    public void NamedGrpcTraceExporterReloadsEndpointWithoutRecreatingProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4317/first",
            ["Protocol"] = "Grpc",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>("trace", configuration);
        services.Configure<OtlpExporterOptions>("trace", options => options.HttpClientFactory = () => new HttpClient(new RecordingHandler(requests)));
        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddSource("grpc-reload-test")
            .AddOtlpExporter("trace", configure: null));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("grpc-reload-test"));

        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first/opentelemetry.proto.collector.trace.v1.TraceService/Export");

        configuration["Endpoint"] = "http://localhost:4317/second";
        configuration.Reload();
        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second/opentelemetry.proto.collector.trace.v1.TraceService/Export");
        Assert.Same(provider, serviceProvider.GetRequiredService<TracerProvider>());

        void Export()
        {
            using (source.StartActivity("test"))
            {
            }

            Assert.True(provider.ForceFlush());
        }
    }

    [Theory]
    [InlineData("Traces")]
    [InlineData("Metrics")]
    [InlineData("Logs")]
    public void ReloadableExportersReturnFailureAfterShutdown(string signal)
    {
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = new ConstructionHttpClient();
        var options = CreateOptions();
        options.HttpClientFactory = () => httpClient;
        Assert.True(Enum.TryParse<OtlpSignalType>(signal, out var signalType));
        using var client = ReloadableExportClient.Create(
            options,
            serviceProvider,
            Options.DefaultName,
            signalType,
            useOtlpExporter: false,
            usesHttpClientFactory: false);
        using var handler = new OtlpExporterTransmissionHandler(client, 10000);
        using var exporter = CreateExporter(signal, handler);
        Assert.True(ShutdownExporter(signal, exporter));
        var result = signal switch
        {
            "Traces" => ((OtlpTraceExporter)exporter).Export(new Batch<Activity>([], 0)),
            "Metrics" => ((OtlpMetricExporter)exporter).Export(new Batch<Metric>([], 0)),
            "Logs" => ((OtlpLogExporter)exporter).Export(new Batch<LogRecord>([], 0)),
            _ => throw new InvalidOperationException("Unexpected signal."),
        };
        Assert.Equal(ExportResult.Failure, result);
        Assert.Equal(0, httpClient.DisposeCount);
    }

    [Theory]
    [InlineData("Traces")]
    [InlineData("Metrics")]
    [InlineData("Logs")]
    public void SkippingRegistrationCheckStillUsesNamedExporterOptions(string signal)
    {
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions);
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        using var serviceProvider = services.BuildServiceProvider();
        var options = monitor.Get("reload");
        using var pipeline = signal switch
        {
            "Traces" => (IDisposable)OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                serviceProvider,
                options,
                new(),
                new(),
                ExportProcessorType.Simple,
                new(),
                skipUseOtlpExporterRegistrationCheck: true,
                optionsName: "reload"),
            "Metrics" => OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                serviceProvider,
                options,
                new(),
                new(),
                skipUseOtlpExporterRegistrationCheck: true,
                optionsName: "reload"),
            "Logs" => OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                serviceProvider,
                options,
                new(),
                new(),
                new(),
                skipUseOtlpExporterRegistrationCheck: true,
                optionsName: "reload"),
            _ => throw new InvalidOperationException("Unexpected signal."),
        };
        Assert.Single(monitor.Listeners);
    }

    [Theory]
    [InlineData("Traces", false)]
    [InlineData("Metrics", false)]
    [InlineData("Logs", false)]
    [InlineData("Traces", true)]
    [InlineData("Metrics", true)]
    [InlineData("Logs", true)]
    public void ExporterDisposesTransmissionHandlerOnce(string signal, bool shutdownFirst)
    {
        using var client = new ControlledExportClient();
        using var handler = new OtlpExporterTransmissionHandler(client, 10000);
        using var exporter = CreateExporter(signal, handler);
        if (shutdownFirst)
        {
            Assert.True(ShutdownExporter(signal, exporter));
        }

        exporter.Dispose();
        exporter.Dispose();
        handler.Dispose();
        Assert.Equal(1, client.DisposeCount);
        Assert.Equal(1, client.ShutdownCount);
    }

    [Theory]
    [InlineData("Traces", false)]
    [InlineData("Metrics", false)]
    [InlineData("Logs", false)]
    [InlineData("Traces", true)]
    [InlineData("Metrics", true)]
    [InlineData("Logs", true)]
    public void DisposeCancelsDiskRetryBeforeReleasingResources(string signal, bool shutdownFirst)
    {
        using var client = new ControlledExportClient();
        using var blobs = new RetryBlobProvider();
        using var handler = new OtlpExporterPersistentStorageTransmissionHandler(blobs, client, 10000);
        using var exporter = CreateExporter(signal, handler);
        Assert.False(handler.InitiateAndWaitForRetryProcess(0));
        try
        {
            Assert.True(client.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            if (shutdownFirst)
            {
                Assert.True(ShutdownExporter(signal, exporter));
            }

            exporter.Dispose();
            Assert.Equal(0, client.ActiveSends);
            Assert.Equal(1, client.DisposeCount);
            Assert.True(blobs.Disposed);
            Assert.False(blobs.Blob.Deleted);
        }
        finally
        {
            client.Shutdown(0);
        }
    }

    [Theory]
    [InlineData("Traces", false)]
    [InlineData("Metrics", false)]
    [InlineData("Logs", false)]
    [InlineData("Traces", true)]
    [InlineData("Metrics", true)]
    [InlineData("Logs", true)]
    public void NamedExporterFailureReleasesOwnedClientsAndSubscriptions(string signal, bool failConfiguration)
    {
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions);
        var factory = new ConstructionHttpClientFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        services.AddSingleton<IHttpClientFactory>(factory);
        using var serviceProvider = services.BuildServiceProvider();

        AssertConstructionFailure(serviceProvider, monitor.Get("reload"), signal, failConfiguration);

        Assert.Empty(monitor.Listeners);
        Assert.All(factory.Clients, client => Assert.Equal(1, client.DisposeCount));
        var clientCount = factory.Clients.Count;
        monitor.Reload("reload");
        Assert.Equal(clientCount, factory.Clients.Count);
    }

    [Theory]
    [InlineData("Traces")]
    [InlineData("Metrics")]
    [InlineData("Logs")]
    public void NamedExporterFailureKeepsCustomHttpClientOwnedByCaller(string signal)
    {
        using var client = new ConstructionHttpClient();
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(() => new()
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () => client,
        });
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        using var serviceProvider = services.BuildServiceProvider();

        AssertConstructionFailure(serviceProvider, monitor.Get("reload"), signal, failConfiguration: false);

        Assert.Empty(monitor.Listeners);
        Assert.Equal(0, client.DisposeCount);
        monitor.Reload("reload");
        Assert.Equal(0, client.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SubscriptionFailureReleasesInitialClientAndEarlierSubscriptions(
        bool builderSubscriptionFails,
        bool ownsHttpClient)
    {
        using var customHttpClient = new ConstructionHttpClient();
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions)
        {
            ThrowOnSubscribe = !builderSubscriptionFails,
        };
        var builderMonitor = new TrackingOptionsMonitor<OtlpExporterBuilderOptions>(
            () => throw new InvalidOperationException("Unexpected options access."))
        {
            ThrowOnSubscribe = builderSubscriptionFails,
        };
        var factory = new ConstructionHttpClientFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        services.AddSingleton<IOptionsMonitor<OtlpExporterBuilderOptions>>(builderMonitor);
        services.AddSingleton<IOptionsMonitorCache<OtlpExporterBuilderOptions>>(new OptionsCache<OtlpExporterBuilderOptions>());
        if (ownsHttpClient)
        {
            services.AddSingleton<IHttpClientFactory>(factory);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = CreateOptions();
        if (!ownsHttpClient)
        {
            options.HttpClientFactory = () => customHttpClient;
        }

        Assert.Equal(
            ownsHttpClient,
            options.TryEnableIHttpClientFactoryIntegration(serviceProvider, OtlpExporterHttpClientNames.TraceExporter));

        Assert.Throws<InvalidOperationException>(() => ReloadableExportClient.Create(
            options,
            serviceProvider,
            "reload",
            OtlpSignalType.Traces,
            useOtlpExporter: true,
            usesHttpClientFactory: ownsHttpClient));

        Assert.Empty(monitor.Listeners);
        Assert.Empty(builderMonitor.Listeners);
        Assert.Equal(0, customHttpClient.DisposeCount);
        if (ownsHttpClient)
        {
            Assert.Equal(1, Assert.Single(factory.Clients).DisposeCount);
        }
        else
        {
            Assert.Empty(factory.Clients);
        }
    }

    [Theory]
    [InlineData("Traces")]
    [InlineData("Metrics")]
    [InlineData("Logs")]
    public void NamedExporterConfigurationFailureStopsDiskRetry(string signal)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"otlp-construction-{Guid.NewGuid():N}");
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions);
        var factory = new ConstructionHttpClientFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        services.AddSingleton<IHttpClientFactory>(factory);
        using var serviceProvider = services.BuildServiceProvider();

        try
        {
            AssertConstructionFailure(serviceProvider, CreateOptions(), signal, failConfiguration: true, directory);
            Assert.Empty(monitor.Listeners);
            Assert.All(factory.Clients, client => Assert.Equal(1, client.DisposeCount));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Assert.Equal(
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetDirectoryName(Path.GetFullPath(directory)));
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static OtlpExporterOptions CreateOptions() => new() { Protocol = OtlpExportProtocol.HttpProtobuf };

    private static IDisposable CreateExporter(string signal, OtlpExporterTransmissionHandler handler) => signal switch
    {
        "Traces" => new OtlpTraceExporter(CreateOptions(), new(), new(), handler),
        "Metrics" => new OtlpMetricExporter(CreateOptions(), new(), handler),
        "Logs" => new OtlpLogExporter(CreateOptions(), new(), new(), handler),
        _ => throw new InvalidOperationException("Unexpected signal."),
    };

    private static bool ShutdownExporter(string signal, IDisposable exporter) => signal switch
    {
        "Traces" => ((OtlpTraceExporter)exporter).Shutdown(),
        "Metrics" => ((OtlpMetricExporter)exporter).Shutdown(),
        "Logs" => ((OtlpLogExporter)exporter).Shutdown(),
        _ => throw new InvalidOperationException("Unexpected signal."),
    };

    private static void AssertConstructionFailure(
        IServiceProvider serviceProvider,
        OtlpExporterOptions options,
        string signal,
        bool failConfiguration,
        string? diskRetryDirectory = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(failConfiguration && diskRetryDirectory is null
            ? []
            : new Dictionary<string, string?>
            {
                [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = diskRetryDirectory ?? "invalid\0path",
            }).Build();
        var experimentalOptions = new ExperimentalOptions(configuration);

        if (failConfiguration)
        {
            Assert.Throws<InvalidOperationException>(Build);
        }
        else
        {
            Assert.ThrowsAny<ArgumentException>(Build);
        }

        void Build()
        {
            using var pipeline = signal switch
            {
                "Traces" => (IDisposable)OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                    serviceProvider,
                    options,
                    new(),
                    experimentalOptions,
                    ExportProcessorType.Simple,
                    new(),
                    configureExporterInstance: failConfiguration ? ThrowConfiguration<Activity> : null,
                    optionsName: "reload"),
                "Metrics" => OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                    serviceProvider,
                    options,
                    new(),
                    experimentalOptions,
                    configureExporterInstance: failConfiguration ? ThrowConfiguration<Metric> : null,
                    optionsName: "reload"),
                "Logs" => OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                    serviceProvider,
                    options,
                    new(),
                    new(),
                    experimentalOptions,
                    configureExporterInstance: failConfiguration ? ThrowConfiguration<LogRecord> : null,
                    optionsName: "reload"),
                _ => throw new InvalidOperationException("Unexpected signal."),
            };
        }

        static BaseExporter<T> ThrowConfiguration<T>(BaseExporter<T> exporter)
            where T : class
        {
            Assert.NotNull(exporter);
            throw new InvalidOperationException("Configuration failed.");
        }
    }

    private sealed class TrackingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        internal List<TrackingHttpClient> Clients { get; } = [];

        internal List<string> Names { get; } = [];

        public HttpClient CreateClient(string name)
        {
            var client = new TrackingHttpClient(handler);
            this.Clients.Add(client);
            this.Names.Add(name);
            return client;
        }
    }

    private sealed class TrackingHttpClient(HttpMessageHandler handler) : HttpClient(handler, disposeHandler: false)
    {
        internal int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingHandler(ConcurrentQueue<(Uri Uri, string Header)> requests, Action<HttpRequestMessage>? observe = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Record(request);
            return Task.FromResult(CreateResponse(request));
        }

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Record(request);
            return CreateResponse(request);
        }
#endif

        private static HttpResponseMessage CreateResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (request.Version.Major == 2)
            {
                response.Headers.TryAddWithoutValidation("grpc-status", "0");
                response.Content = new ByteArrayContent([0, 0, 0, 0, 0]);
            }

            return response;
        }

        private void Record(HttpRequestMessage request)
        {
            observe?.Invoke(request);
            request.Headers.TryGetValues("test", out var values);
            requests.Enqueue((request.RequestUri!, values?.FirstOrDefault() ?? string.Empty));
        }
    }

    private sealed class BlockingHandler(
        ConcurrentQueue<(Uri Uri, string Header)> requests,
        ManualResetEventSlim entered,
        ManualResetEventSlim release) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.Run(() => this.SendCore(request, cancellationToken), cancellationToken);

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.SendCore(request, cancellationToken);
#endif

        private HttpResponseMessage SendCore(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue((request.RequestUri!, string.Empty));
            entered.Set();
            release.Wait(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ControlledExportClient : IExportClient, IDisposable
    {
        private readonly CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        private int activeSends;

        internal ManualResetEventSlim Entered { get; } = new(false);

        internal int ActiveSends => Volatile.Read(ref this.activeSends);

        internal int DisposeCount { get; private set; }

        internal int ShutdownCount { get; private set; }

        public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.activeSends);
            try
            {
                this.Entered.Set();
                this.cancellation.Token.WaitHandle.WaitOne();
                return new ExportClientHttpResponse(false, deadlineUtc, null, null);
            }
            finally
            {
                Interlocked.Decrement(ref this.activeSends);
            }
        }

        public bool Shutdown(int timeoutMilliseconds)
        {
            this.ShutdownCount++;
            if (this.DisposeCount == 0)
            {
                this.cancellation.Cancel();
            }

            return true;
        }

        public void Dispose()
        {
            if (++this.DisposeCount == 1)
            {
                this.cancellation.Cancel();
                this.cancellation.Dispose();
                this.Entered.Dispose();
            }
        }
    }

    private sealed class RetryBlobProvider : PersistentBlobProvider, IDisposable
    {
        private bool available = true;

        internal bool Disposed { get; private set; }

        internal RetryBlob Blob { get; } = new();

        public void Dispose() => this.Disposed = true;

        protected override IEnumerable<PersistentBlob> OnGetBlobs() => [];

        protected override bool OnTryGetBlob(out PersistentBlob blob)
        {
            blob = this.Blob;
            var result = this.available;
            this.available = false;
            return result;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, out PersistentBlob blob) => this.OnTryGetBlob(out blob);

        protected override bool OnTryCreateBlob(byte[] buffer, out PersistentBlob blob) => this.OnTryGetBlob(out blob);
    }

    private sealed class RetryBlob : PersistentBlob
    {
        internal bool Deleted { get; private set; }

        protected override bool OnTryRead(out byte[] buffer)
        {
            buffer = [1];
            return true;
        }

        protected override bool OnTryLease(int leasePeriodMilliseconds) => true;

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0) => true;

        protected override bool OnTryDelete()
        {
            this.Deleted = true;
            return true;
        }
    }

    private sealed class TrackingOptionsMonitor<T>(Func<T> createOptions) : IOptionsMonitor<T>
        where T : class
    {
        private readonly Dictionary<string, T> cache = [];

        public T CurrentValue => this.Get(Options.DefaultName);

        internal List<Action<T, string?>> Listeners { get; } = [];

        internal bool ThrowOnSubscribe { get; set; }

        public T Get(string? name)
        {
            name ??= Options.DefaultName;
            if (!this.cache.TryGetValue(name, out var options))
            {
                options = createOptions();
                this.cache.Add(name, options);
            }

            return options;
        }

        public IDisposable OnChange(Action<T, string?> listener)
        {
            if (this.ThrowOnSubscribe)
            {
                throw new InvalidOperationException("Subscription failed.");
            }

            this.Listeners.Add(listener);
            return new Subscription(this.Listeners, listener);
        }

        internal void Reload(string name)
        {
            this.cache.Remove(name);
            var options = this.Get(name);
            foreach (var listener in this.Listeners.ToArray())
            {
                listener(options, name);
            }
        }

        private sealed class Subscription(List<Action<T, string?>> listeners, Action<T, string?> listener) : IDisposable
        {
            public void Dispose() => listeners.Remove(listener);
        }
    }

    private sealed class ConstructionHttpClientFactory : IHttpClientFactory
    {
        internal List<ConstructionHttpClient> Clients { get; } = [];

        public HttpClient CreateClient(string name)
        {
            var client = new ConstructionHttpClient();
            this.Clients.Add(client);
            return client;
        }
    }

    private sealed class ConstructionHttpClient : HttpClient
    {
        internal int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}
