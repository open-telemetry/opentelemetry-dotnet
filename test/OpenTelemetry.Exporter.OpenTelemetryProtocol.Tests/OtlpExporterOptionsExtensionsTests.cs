// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExporterOptionsExtensionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetHeaders_NoOptionHeaders_ReturnsStandardHeaders(string? optionHeaders)
    {
        var options = new OtlpExporterOptions
        {
            Headers = optionHeaders,
        };

        var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

        Assert.Equal(OtlpExporterOptions.StandardHeaders.Length, headers.Count);

        for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
        {
            Assert.Contains(headers, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key && entry.Value == OtlpExporterOptions.StandardHeaders[i].Value);
        }
    }

    [Theory]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient))]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient))]
    public void GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient(OtlpExportProtocol protocol, Type expectedExportClientType)
    {
        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
        };

        var exportClient = options.GetExportClient(OtlpSignalType.Traces);

        Assert.Equal(expectedExportClientType, exportClient.GetType());
    }

    [Fact]
    public void GetTraceExportClient_UnsupportedProtocol_Throws()
    {
        var options = new OtlpExporterOptions
        {
            Protocol = (OtlpExportProtocol)123,
        };

        Assert.Throws<NotSupportedException>(() => options.GetExportClient(OtlpSignalType.Traces));
    }

    [Theory]
    [InlineData("http://test:8888", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/v1/traces", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/v1/traces/", "http://test:8888/v1/traces/")]
    public void AppendPathIfNotPresent_TracesPath_AppendsCorrectly(string inputUri, string expectedUri)
    {
        var uri = new Uri(inputUri, UriKind.Absolute);

        var resultUri = uri.AppendPathIfNotPresent("v1/traces");

        Assert.Equal(expectedUri, resultUri.AbsoluteUri);
    }

    [Theory]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, "disk")]
    public void GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue(OtlpExportProtocol protocol, Type exportClientType, bool customHttpClient, int expectedTimeoutMilliseconds, string? retryStrategy)
    {
        var exporterOptions = new OtlpExporterOptions() { Protocol = protocol };
        if (customHttpClient)
        {
            exporterOptions.HttpClientFactory = () =>
            {
                return new HttpClient { Timeout = TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds) };
            };
        }

        var configuration = new ConfigurationBuilder()
         .AddInMemoryCollection(new Dictionary<string, string?> { [ExperimentalOptions.OtlpRetryEnvVar] = retryStrategy })
         .Build();

        var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), OtlpSignalType.Traces);
        AssertTransmissionHandler(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, retryStrategy);
    }

    private static void AssertTransmissionHandler(OtlpExporterTransmissionHandler transmissionHandler, Type exportClientType, int expectedTimeoutMilliseconds, string? retryStrategy)
    {
        if (retryStrategy == "in_memory")
        {
            Assert.True(transmissionHandler is OtlpExporterRetryTransmissionHandler);
        }
        else if (retryStrategy == "disk")
        {
            Assert.True(transmissionHandler is OtlpExporterPersistentStorageTransmissionHandler);
        }
        else
        {
            Assert.True(transmissionHandler is OtlpExporterTransmissionHandler);
        }

        Assert.Equal(exportClientType, transmissionHandler.ExportClient.GetType());

        Assert.Equal(expectedTimeoutMilliseconds, transmissionHandler.TimeoutMilliseconds);
    }
}
