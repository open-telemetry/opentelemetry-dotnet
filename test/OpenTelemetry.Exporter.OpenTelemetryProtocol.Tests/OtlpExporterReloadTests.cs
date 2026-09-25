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
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpExporterReloadTests
{
    [Fact]
    public void NamedTraceExporterReloadsEndpointAndHeadersWithoutRecreatingProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "http://localhost:4318/first",
            ["Protocol"] = "HttpProtobuf",
            ["Headers"] = "test=first",
        }).Build();
        var requests = new ConcurrentQueue<(Uri Uri, string Header)>();
        var services = new ServiceCollection();
        services.AddHttpClient("OtlpTraceExporter").ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));
        services.Configure<OtlpExporterOptions>("trace", configuration);
        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddSource("reload-test")
            .AddOtlpExporter("trace", configure: null));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(new ActivitySourceOptions("reload-test"));

        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/first" && r.Header == "first");

        configuration["Endpoint"] = "http://localhost:4318/second";
        configuration["Headers"] = "test=second";
        configuration.Reload();
        Export();
        Assert.Contains(requests, r => r.Uri.AbsolutePath == "/second" && r.Header == "second");

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
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            configuration["Endpoint"] = "http://localhost:4318/second";
            var reload = Task.Run(configuration.Reload);
            Assert.Same(reload, await Task.WhenAny(reload, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true));
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

    private sealed class RecordingHandler(ConcurrentQueue<(Uri Uri, string Header)> requests) : HttpMessageHandler
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
}
