// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
