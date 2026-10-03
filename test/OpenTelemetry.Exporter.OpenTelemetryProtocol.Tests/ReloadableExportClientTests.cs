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
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () => httpClient,
        };
        using var client = ReloadableExportClient.Create(
            options,
            serviceProvider,
            Options.DefaultName,
            OtlpSignalType.Traces,
            useOtlpExporter: false,
            usesHttpClientFactory: false);

        Assert.True(client.Shutdown(Timeout.Infinite));
        Assert.Throws<InvalidOperationException>(() => client.SendExportRequest(
            [],
            0,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken));
    }
}
