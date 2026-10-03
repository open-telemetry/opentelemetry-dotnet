// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpExporterConstructionTests
{
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
        var factory = new TrackingHttpClientFactory();
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
        using var client = new TrackingHttpClient();
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

    [Fact]
    public void SubscriptionFailureReleasesInitialClientAndEarlierSubscription()
    {
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions) { ThrowOnSubscribe = true };
        var builderMonitor = new TrackingOptionsMonitor<OtlpExporterBuilderOptions>(
            () => throw new InvalidOperationException("Unexpected options access."));
        var factory = new TrackingHttpClientFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        services.AddSingleton<IOptionsMonitor<OtlpExporterBuilderOptions>>(builderMonitor);
        services.AddSingleton<IOptionsMonitorCache<OtlpExporterBuilderOptions>>(new OptionsCache<OtlpExporterBuilderOptions>());
        services.AddSingleton<IHttpClientFactory>(factory);
        using var serviceProvider = services.BuildServiceProvider();
        var options = CreateOptions();
        Assert.True(options.TryEnableIHttpClientFactoryIntegration(serviceProvider, OtlpExporterHttpClientNames.TraceExporter));

        Assert.Throws<InvalidOperationException>(() => ReloadableExportClient.Create(
            options,
            serviceProvider,
            "reload",
            OtlpSignalType.Traces,
            useOtlpExporter: true,
            usesHttpClientFactory: true));

        Assert.Empty(builderMonitor.Listeners);
        Assert.Equal(1, Assert.Single(factory.Clients).DisposeCount);
    }

    [Theory]
    [InlineData("Traces")]
    [InlineData("Metrics")]
    [InlineData("Logs")]
    public void NamedExporterConfigurationFailureStopsDiskRetry(string signal)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"otlp-construction-{Guid.NewGuid():N}");
        var monitor = new TrackingOptionsMonitor<OtlpExporterOptions>(CreateOptions);
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(monitor);
        using var serviceProvider = services.BuildServiceProvider();

        try
        {
            AssertConstructionFailure(serviceProvider, CreateOptions(), signal, failConfiguration: true, directory);
            Assert.Empty(monitor.Listeners);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static OtlpExporterOptions CreateOptions() => new() { Protocol = OtlpExportProtocol.HttpProtobuf };

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
        Thread? retryThread = null;

        if (failConfiguration)
        {
            Assert.Throws<InvalidOperationException>(Build);
        }
        else
        {
            Assert.ThrowsAny<ArgumentException>(Build);
        }

        if (diskRetryDirectory != null)
        {
            Assert.NotNull(retryThread);
            Assert.False(retryThread.IsAlive);
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

        BaseExporter<T> ThrowConfiguration<T>(BaseExporter<T> exporter)
            where T : class
        {
            if (diskRetryDirectory != null)
            {
                var exporterType = signal switch
                {
                    "Traces" => typeof(OtlpTraceExporter),
                    "Metrics" => typeof(OtlpMetricExporter),
                    "Logs" => typeof(OtlpLogExporter),
                    _ => throw new InvalidOperationException("Unexpected signal."),
                };
                var handler = Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(
                    exporterType.GetField("transmissionHandler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(exporter));
                retryThread = Assert.IsType<Thread>(
                    typeof(OtlpExporterPersistentStorageTransmissionHandler).GetField("thread", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(handler));
            }

            throw new InvalidOperationException("Configuration failed.");
        }
    }

    private sealed class TrackingOptionsMonitor<T>(Func<T> createOptions) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => this.Get(Options.DefaultName);

        internal List<Action<T, string?>> Listeners { get; } = [];

        internal bool ThrowOnSubscribe { get; set; }

        public T Get(string? name) => createOptions();

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
            foreach (var listener in this.Listeners.ToArray())
            {
                listener(this.Get(name), name);
            }
        }

        private sealed class Subscription(List<Action<T, string?>> listeners, Action<T, string?> listener) : IDisposable
        {
            public void Dispose() => listeners.Remove(listener);
        }
    }

    private sealed class TrackingHttpClientFactory : IHttpClientFactory
    {
        internal List<TrackingHttpClient> Clients { get; } = [];

        public HttpClient CreateClient(string name)
        {
            var client = new TrackingHttpClient();
            this.Clients.Add(client);
            return client;
        }
    }

    private sealed class TrackingHttpClient : HttpClient
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
