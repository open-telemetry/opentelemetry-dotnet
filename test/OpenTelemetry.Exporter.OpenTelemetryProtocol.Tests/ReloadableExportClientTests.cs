// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class ReloadableExportClientTests
{
    [Fact]
    public async Task ShutdownDuringInitialLogClientCreationCancelsTheSend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var requestEntered = new SemaphoreSlim(0);
        using var handler = new CancellationTrackingHandler(requestEntered, release, cancellationToken);
        using var httpClient = new TrackingHttpClient(handler);
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(() =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            return httpClient;
        }));
        using var serviceProvider = services.BuildServiceProvider();
        var options = new OtlpExporterOptions { Protocol = OtlpExportProtocol.HttpProtobuf };
        Assert.True(options.TryEnableIHttpClientFactoryIntegration(serviceProvider, OtlpExporterHttpClientNames.LogExporter));
        using var client = ReloadableExportClient.Create(options, serviceProvider, "logs", OtlpSignalType.Logs, false, true);
        var send = Task.Run(() => client.SendExportRequest([], 0, DateTime.UtcNow.AddSeconds(30), cancellationToken), cancellationToken);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            Assert.True(client.Shutdown(0));
        }
        finally
        {
            release.Set();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await send.ConfigureAwait(true));
        Assert.Equal(1, httpClient.DisposeCount);
    }

    [Fact]
    public void ReloadFailureIdentifiesSignalAndOptionsNameAndKeepsPreviousClient()
    {
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Protocol"] = "HttpProtobuf",
            ["Headers"] = "test=before",
        }).Build();
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>("reload-fails", configuration);
        services.Configure<OtlpExporterOptions>("reload-fails", options => options.HttpClientFactory = () => httpClient);
        services.Configure<OtlpExporterOptions>("unaffected", options =>
        {
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Headers = "test=good";
            options.HttpClientFactory = () => httpClient;
        });
        using var serviceProvider = services.BuildServiceProvider();
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();
        using var failedClient = ReloadableExportClient.Create(monitor.Get("reload-fails").Copy(), serviceProvider, "reload-fails", OtlpSignalType.Metrics, false, false);
        using var unaffectedClient = ReloadableExportClient.Create(monitor.Get("unaffected").Copy(), serviceProvider, "unaffected", OtlpSignalType.Traces, false, false);
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        configuration["Headers"] = "invalid";
        configuration.Reload();
        var warning = Assert.Single(listener.Messages, message => message.EventId == 42);
        Assert.NotNull(warning.Payload);
        Assert.Equal("Metrics", warning.Payload[0]);
        Assert.Equal("reload-fails", warning.Payload[1]);
        Assert.IsType<string>(warning.Payload[2]);

        Assert.True(failedClient.SendExportRequest([], 0, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken).Success);
        Assert.Equal("before", Assert.Single(handler.HttpRequestMessage!.Headers.GetValues("test")));
        Assert.True(unaffectedClient.SendExportRequest([], 0, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken).Success);
        Assert.Equal("good", Assert.Single(handler.HttpRequestMessage.Headers.GetValues("test")));
    }

    [Theory]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 4, false)]
    [InlineData(true, 4, false)]
    [InlineData(false, 2, true)]
    [InlineData(false, 4, true)]
    public async Task ShutdownCancelsRequestsFromEveryLiveGeneration(bool ownsHttpClient, int generations, bool previousClientDisposed)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim();
        using var handler = new CancellationTrackingHandler(entered, release, cancellationToken);
        var httpClients = new List<TrackingHttpClient>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>("generations", configuration);
        if (ownsHttpClient)
        {
            services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(CreateHttpClient));
        }
        else
        {
            services.Configure<OtlpExporterOptions>("generations", options => options.HttpClientFactory = CreateHttpClient);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("generations").Copy();
        var usesFactory = options.TryEnableIHttpClientFactoryIntegration(serviceProvider, OtlpExporterHttpClientNames.TraceExporter);
        using var client = ReloadableExportClient.Create(options, serviceProvider, "generations", OtlpSignalType.Traces, false, usesFactory);
        var sends = new List<Task>();
        try
        {
            for (var generation = 0; generation < generations; generation++)
            {
                if (generation > 0)
                {
                    configuration.Reload();
                }

                sends.Add(Task.Run(
                    () => Assert.ThrowsAny<OperationCanceledException>(() => client.SendExportRequest([], 0, DateTime.UtcNow.AddSeconds(30), cancellationToken)),
                    cancellationToken));
                Assert.True(await entered.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(true));
            }

            if (previousClientDisposed)
            {
                httpClients[0].Dispose();
                Assert.Throws<ObjectDisposedException>(() => client.Shutdown(0));
            }
            else
            {
                Assert.True(client.Shutdown(0));
            }

            Assert.Equal(generations, handler.CancellationCount);
            Assert.All(httpClients.Skip(previousClientDisposed ? 1 : 0), httpClient => Assert.Equal(0, httpClient.DisposeCount));
            release.Set();
            await Task.WhenAll(sends).ConfigureAwait(true);
            for (var index = 0; index < httpClients.Count; index++)
            {
                Assert.Equal(ownsHttpClient || (previousClientDisposed && index == 0) ? 1 : 0, httpClients[index].DisposeCount);
            }
        }
        finally
        {
            release.Set();
            await Task.WhenAll(sends).ConfigureAwait(true);
            foreach (var httpClient in httpClients)
            {
                httpClient.Dispose();
            }
        }

        HttpClient CreateHttpClient()
        {
            var httpClient = new TrackingHttpClient(handler);
            httpClients.Add(httpClient);
            return httpClient;
        }
    }

    [Fact]
    public void SendAfterShutdownThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = new HttpClient();
        using var client = CreateClient(serviceProvider, httpClient);

        Assert.True(client.Shutdown(Timeout.Infinite));
        Assert.Throws<InvalidOperationException>(() => client.SendExportRequest(
            [],
            0,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SendAfterDisposeThrowsObjectDisposedException(bool shutdownFirst)
    {
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = new HttpClient();
        using var client = CreateClient(serviceProvider, httpClient);

        if (shutdownFirst)
        {
            Assert.True(client.Shutdown(Timeout.Infinite));
        }

        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.SendExportRequest(
            [],
            0,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RepeatedDisposePreservesHttpClientOwnership(bool shutdownFirst, bool ownsHttpClient)
    {
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = new TrackingHttpClient();
        using var client = CreateClient(serviceProvider, httpClient, ownsHttpClient);

        if (shutdownFirst)
        {
            Assert.True(client.Shutdown(Timeout.Infinite));
        }

        client.Dispose();
        client.Dispose();
        Assert.True(client.Shutdown(Timeout.Infinite));

        Assert.Equal(ownsHttpClient ? 1 : 0, httpClient.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownDefersHttpClientDisposalUntilAllSendsFinish(bool ownsHttpClient)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new CountdownEvent(2);
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var handler = new BlockingHandler(entered, [releaseFirst, releaseSecond], cancellationToken);
        using var httpClient = new TrackingHttpClient(handler);
        using var client = CreateClient(serviceProvider, httpClient, ownsHttpClient);
        var sends = new[]
        {
            Task.Run(Send, cancellationToken),
            Task.Run(Send, cancellationToken),
        };

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            Assert.True(client.Shutdown(Timeout.Infinite));
            Assert.Equal(0, httpClient.DisposeCount);

            releaseFirst.Set();
            var timeout = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            var completed = await Task.WhenAny(sends[0], sends[1], timeout).ConfigureAwait(true);
            Assert.NotSame(timeout, completed);
            await completed.ConfigureAwait(true);
            Assert.Equal(0, httpClient.DisposeCount);

            releaseSecond.Set();
            await Task.WhenAll(sends).ConfigureAwait(true);
            Assert.Equal(ownsHttpClient ? 1 : 0, httpClient.DisposeCount);
            client.Dispose();
            Assert.Equal(ownsHttpClient ? 1 : 0, httpClient.DisposeCount);
        }
        finally
        {
            releaseFirst.Set();
            releaseSecond.Set();
            await Task.WhenAll(sends).ConfigureAwait(true);
        }

        ExportClientResponse Send() => client.SendExportRequest([], 0, DateTime.UtcNow.AddSeconds(30), cancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledSendReleasesItsReference(bool ownsHttpClient)
    {
        var services = new ServiceCollection();
        services.AddOptions<OtlpExporterOptions>();
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = new TrackingHttpClient();
        using var client = CreateClient(serviceProvider, httpClient, ownsHttpClient);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => client.SendExportRequest(
            [],
            0,
            DateTime.UtcNow.AddSeconds(30),
            cancellation.Token));
        Assert.True(client.Shutdown(Timeout.Infinite));
        Assert.Equal(ownsHttpClient ? 1 : 0, httpClient.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StopDuringReloadDiscardsReplacementAndUnsubscribes(bool dispose, bool ownsHttpClient)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var initialHttpClient = new TrackingHttpClient();
        using var replacementHttpClient = new TrackingHttpClient();
        var factoryCalls = 0;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Protocol"] = "HttpProtobuf",
        }).Build();
        var services = new ServiceCollection();
        services.Configure<OtlpExporterOptions>("reload", configuration);
        if (ownsHttpClient)
        {
            services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(CreateHttpClient));
        }
        else
        {
            services.Configure<OtlpExporterOptions>("reload", options => options.HttpClientFactory = CreateHttpClient);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("reload");
        var usesHttpClientFactory = options.TryEnableIHttpClientFactoryIntegration(
            serviceProvider,
            OtlpExporterHttpClientNames.TraceExporter);
        Assert.Equal(ownsHttpClient, usesHttpClientFactory);
        using var client = ReloadableExportClient.Create(
            options,
            serviceProvider,
            "reload",
            OtlpSignalType.Traces,
            useOtlpExporter: false,
            usesHttpClientFactory);
        Assert.Equal(1, factoryCalls);
        var reload = Task.Run(configuration.Reload, cancellationToken);

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            if (dispose)
            {
                client.Dispose();
            }
            else
            {
                Assert.True(client.Shutdown(Timeout.Infinite));
            }

            Assert.Equal(ownsHttpClient ? 1 : 0, initialHttpClient.DisposeCount);
            Assert.Equal(0, replacementHttpClient.DisposeCount);
        }
        finally
        {
            release.Set();
            Assert.Same(reload, await Task.WhenAny(reload, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)).ConfigureAwait(true));
            await reload.ConfigureAwait(true);
        }

        Assert.Equal(ownsHttpClient ? 1 : 0, replacementHttpClient.DisposeCount);
        configuration.Reload();
        Assert.Equal(2, factoryCalls);
        client.Dispose();
        Assert.Equal(ownsHttpClient ? 1 : 0, initialHttpClient.DisposeCount);
        Assert.Equal(ownsHttpClient ? 1 : 0, replacementHttpClient.DisposeCount);

        HttpClient CreateHttpClient()
        {
            if (Interlocked.Increment(ref factoryCalls) == 1)
            {
                return initialHttpClient;
            }

            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            return replacementHttpClient;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadCallbacksUsePrivateOptionsEvenWhenMonitorReturnsCachedInstance(bool useOtlpExporter)
    {
        using var httpClient = new HttpClient();
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            Headers = "test=common",
            HttpClientFactory = () => httpClient,
        };
        var configuration = new ConfigurationBuilder().Build();
        var builderOptions = new OtlpExporterBuilderOptions(
            configuration,
            options,
            new SdkLimitOptions(configuration),
            new ExperimentalOptions(configuration),
            null,
            null,
            new ActivityExportProcessorOptions());
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(new TestOptionsMonitor<OtlpExporterOptions>(options));
        services.AddSingleton<IOptionsMonitor<OtlpExporterBuilderOptions>>(new TestOptionsMonitor<OtlpExporterBuilderOptions>(builderOptions));
        services.AddSingleton<IOptionsMonitorCache<OtlpExporterBuilderOptions>>(new OptionsCache<OtlpExporterBuilderOptions>());
        using var serviceProvider = services.BuildServiceProvider();
        foreach (var signal in new[] { OtlpSignalType.Traces, OtlpSignalType.Metrics, OtlpSignalType.Logs })
        {
            var snapshots = new List<OtlpExporterOptions>();
            using var registration = new OtlpExportClientRegistration(options, serviceProvider, "shared", signal, useOtlpExporter, copy =>
            {
                copy.Headers += $",signal={signal}";
                copy.BatchExportProcessorOptions.ScheduledDelayMilliseconds++;
                snapshots.Add(copy);
            });
            for (var generation = 0; generation < 2; generation++)
            {
                var created = registration.CreateExportClient();
                Assert.NotNull(created);
                Assert.False(created.Value.OwnsHttpClient);
                Assert.Equal($"test=common,signal={signal}", snapshots[generation].Headers);
                Assert.Equal(options.BatchExportProcessorOptions.ScheduledDelayMilliseconds + 1, snapshots[generation].BatchExportProcessorOptions.ScheduledDelayMilliseconds);
                Assert.Equal("test=common", options.Headers);
                created.Value.Client.Shutdown(0);
            }

            Assert.NotSame(snapshots[0], snapshots[1]);
            Assert.NotSame(snapshots[0].BatchExportProcessorOptions, snapshots[1].BatchExportProcessorOptions);
        }
    }

    [Theory]
    [InlineData(false, "reload", true)]
    [InlineData(true, "reload", true)]
    [InlineData(false, "other", false)]
    [InlineData(true, "other", false)]
    [InlineData(false, "Reload", false)]
    [InlineData(true, "Reload", false)]
    [InlineData(false, null, false)]
    [InlineData(true, null, false)]
    public void OptionsCallbacksFilterNamesAndPreserveCacheInvalidation(bool builderChange, string? changedName, bool shouldReload)
    {
        using var httpClient = new HttpClient();
        var cache = new TrackingOptionsCache<OtlpExporterBuilderOptions>();
        var factoryCalls = 0;
        var invalidationsAtLastCreation = 0;
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () =>
            {
                factoryCalls++;
                invalidationsAtLastCreation = cache.RemovedNames.Count;
                return httpClient;
            },
        };
        var configuration = new ConfigurationBuilder().Build();
        var builderOptions = new OtlpExporterBuilderOptions(
            configuration,
            options,
            new SdkLimitOptions(configuration),
            new ExperimentalOptions(configuration),
            logRecordExportProcessorOptions: null,
            metricReaderOptions: null,
            new ActivityExportProcessorOptions());
        builderOptions.TracingOptionsInstance.Protocol = OtlpExportProtocol.HttpProtobuf;
        builderOptions.TracingOptionsInstance.HttpClientFactory = options.HttpClientFactory;
        var optionsMonitor = new TestOptionsMonitor<OtlpExporterOptions>(options);
        var builderMonitor = new TestOptionsMonitor<OtlpExporterBuilderOptions>(builderOptions);
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(optionsMonitor);
        services.AddSingleton<IOptionsMonitor<OtlpExporterBuilderOptions>>(builderMonitor);
        services.AddSingleton<IOptionsMonitorCache<OtlpExporterBuilderOptions>>(cache);
        using var serviceProvider = services.BuildServiceProvider();
        using var client = ReloadableExportClient.Create(
            options,
            serviceProvider,
            "reload",
            OtlpSignalType.Traces,
            useOtlpExporter: true,
            usesHttpClientFactory: false);
        Assert.Equal(1, factoryCalls);

        if (builderChange)
        {
            builderMonitor.Notify(changedName);
        }
        else
        {
            optionsMonitor.Notify(changedName);
        }

        var expectedInvalidations = shouldReload && !builderChange ? 1 : 0;
        Assert.Equal(shouldReload ? 2 : 1, factoryCalls);
        Assert.Equal(expectedInvalidations, cache.RemovedNames.Count);
        Assert.Equal(expectedInvalidations, invalidationsAtLastCreation);
        if (expectedInvalidations == 1)
        {
            Assert.Equal("reload", Assert.Single(cache.RemovedNames));
        }
    }

    private static ReloadableExportClient CreateClient(IServiceProvider serviceProvider, HttpClient httpClient, bool ownsHttpClient = false)
    {
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () => httpClient,
        };

        return ReloadableExportClient.Create(
            options,
            serviceProvider,
            Options.DefaultName,
            OtlpSignalType.Traces,
            useOtlpExporter: false,
            usesHttpClientFactory: ownsHttpClient);
    }

    private sealed class TestHttpClientFactory(Func<HttpClient> createClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => createClient();
    }

    private sealed class TrackingHttpClient : HttpClient
    {
        private int disposeCount;

        internal TrackingHttpClient()
        {
        }

        internal TrackingHttpClient(HttpMessageHandler handler)
            : base(handler, disposeHandler: false)
        {
        }

        internal int DisposeCount => Volatile.Read(ref this.disposeCount);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref this.disposeCount);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class CancellationTrackingHandler(SemaphoreSlim entered, ManualResetEventSlim release, CancellationToken testCancellationToken) : HttpMessageHandler
    {
        private int cancellationCount;

        internal int CancellationCount => Volatile.Read(ref this.cancellationCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.Run(() => this.SendCore(cancellationToken), testCancellationToken);

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.SendCore(cancellationToken);
#endif

        private HttpResponseMessage SendCore(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => Interlocked.Increment(ref this.cancellationCount));
            entered.Release();
            release.Wait(testCancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class BlockingHandler(CountdownEvent entered, ManualResetEventSlim[] releases, CancellationToken testCancellationToken) : HttpMessageHandler
    {
        private int nextSend;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.SendCore());

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.SendCore();
#endif

        private HttpResponseMessage SendCore()
        {
            var release = releases[Interlocked.Increment(ref this.nextSend) - 1];
            entered.Signal();

            // Keep the request pending after Shutdown cancels its token, until the test releases it.
            release.Wait(testCancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class TestOptionsMonitor<T>(T options) : IOptionsMonitor<T>
        where T : class
    {
        private Action<T, string?>? listener;

        public T CurrentValue => options;

        public T Get(string? name) => options;

        public IDisposable OnChange(Action<T, string?> listener)
        {
            this.listener = listener;
            return new Subscription(this);
        }

        internal void Notify(string? name) => this.listener?.Invoke(options, name);

        private sealed class Subscription(TestOptionsMonitor<T> monitor) : IDisposable
        {
            public void Dispose() => monitor.listener = null;
        }
    }

    private sealed class TrackingOptionsCache<T> : IOptionsMonitorCache<T>
        where T : class
    {
        private readonly OptionsCache<T> cache = new();

        internal List<string?> RemovedNames { get; } = [];

        public T GetOrAdd(string? name, Func<T> createOptions) => this.cache.GetOrAdd(name, createOptions);

        public bool TryAdd(string? name, T options) => this.cache.TryAdd(name, options);

        public bool TryRemove(string? name)
        {
            this.RemovedNames.Add(name);
            return this.cache.TryRemove(name);
        }

        public void Clear() => this.cache.Clear();
    }
}
