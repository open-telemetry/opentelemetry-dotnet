// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class ReloadableExportClientTests
{
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
}
