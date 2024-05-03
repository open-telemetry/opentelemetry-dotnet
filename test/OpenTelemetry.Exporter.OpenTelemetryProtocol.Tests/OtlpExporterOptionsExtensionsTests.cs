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
using Xunit.Sdk;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExporterOptionsExtensionsTests
{
    [Theory]
    [InlineData("key=value", new string[] { "key" }, new string[] { "value" })]
    [InlineData("key1=value1,key2=value2", new string[] { "key1", "key2" }, new string[] { "value1", "value2" })]
    [InlineData("key1 = value1, key2=value2 ", new string[] { "key1", "key2" }, new string[] { "value1", "value2" })]
    [InlineData("key==value", new string[] { "key" }, new string[] { "=value" })]
    [InlineData("access-token=abc=/123,timeout=1234", new string[] { "access-token", "timeout" }, new string[] { "abc=/123", "1234" })]
    [InlineData("key1=value1;key2=value2", new string[] { "key1" }, new string[] { "value1;key2=value2" })] // semicolon is not treated as a delimiter (https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables)
    [InlineData("Authorization=Basic%20AAA", new string[] { "authorization" }, new string[] { "Basic AAA" })]
    [InlineData("Authorization=Basic AAA", new string[] { "authorization" }, new string[] { "Basic AAA" })]
    public void GetMetadataFromHeadersWorksCorrectFormat(string headers, string[] keys, string[] values)
    {
        var options = new OtlpExporterOptions
        {
            Headers = headers,
        };
        var metadata = options.GetMetadataFromHeaders();

        Assert.Equal(OtlpExporterOptions.StandardHeaders.Length + keys.Length, metadata.Count);

        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Contains(metadata, entry => entry.Key == keys[i] && entry.Value == values[i]);
        }

        for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
        {
            // Metadata key is always converted to lowercase.
            // See: https://cloud.google.com/dotnet/docs/reference/Grpc.Core/latest/Grpc.Core.Metadata.Entry#Grpc_Core_Metadata_Entry__ctor_System_String_System_String_
            Assert.Contains(metadata, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key.ToLower() && entry.Value == OtlpExporterOptions.StandardHeaders[i].Value);
        }
    }

    [Theory]
    [InlineData("headers")]
    [InlineData("key,value")]
    public void GetMetadataFromHeadersThrowsExceptionOnInvalidFormat(string headers)
    {
        try
        {
            var options = new OtlpExporterOptions
            {
                Headers = headers,
            };
            var metadata = options.GetMetadataFromHeaders();
        }
        catch (Exception ex)
        {
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("Headers provided in an invalid format.", ex.Message);
            return;
        }

        throw new XunitException("GetMetadataFromHeaders did not throw an exception for invalid input headers");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetHeaders_NoOptionHeaders_ReturnsStandardHeaders(string optionHeaders)
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
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcTraceExportClient))]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient))]
    public void GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient(OtlpExportProtocol protocol, Type expectedExportClientType)
    {
        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
        };

        var exportClient = options.GetTraceExportClient();

        Assert.Equal(expectedExportClientType, exportClient.GetType());
    }

    [Fact]
    public void GetTraceExportClient_UnsupportedProtocol_Throws()
    {
        var options = new OtlpExporterOptions
        {
            Protocol = (OtlpExportProtocol)123,
        };

        Assert.Throws<NotSupportedException>(() => options.GetTraceExportClient());
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
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcTraceExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcMetricsExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcLogExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcTraceExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), true, 8000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcMetricsExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), true, 8000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcLogExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), true, 8000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcTraceExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpTraceExportClient), true, 8000, "disk")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcMetricsExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpMetricsExportClient), true, 8000, "disk")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcLogExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), false, 10000, "disk")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpLogExportClient), true, 8000, "disk")]
    public void GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue(OtlpExportProtocol protocol, Type exportClientType, bool customHttpClient, int expectedTimeoutMilliseconds, string retryStrategy)
    {
        var exporterOptions = new OtlpExporterOptions() { Protocol = protocol };
        if (customHttpClient)
        {
            exporterOptions.HttpClientFactory = () =>
            {
                return new HttpClient() { Timeout = TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds) };
            };
        }

        var configuration = new ConfigurationBuilder()
         .AddInMemoryCollection(new Dictionary<string, string> { [ExperimentalOptions.OtlpRetryEnvVar] = retryStrategy, [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = "/" })
         .Build();

        if (exportClientType == typeof(OtlpGrpcTraceExportClient) || exportClientType == typeof(OtlpHttpTraceExportClient))
        {
            var transmissionHandler = exporterOptions.GetTraceExportTransmissionHandler(new ExperimentalOptions(configuration));

            AssertTransmissionHandlerProperties(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, retryStrategy);
        }
        else if (exportClientType == typeof(OtlpGrpcMetricsExportClient) || exportClientType == typeof(OtlpHttpMetricsExportClient))
        {
            var transmissionHandler = exporterOptions.GetMetricsExportTransmissionHandler(new ExperimentalOptions(configuration));

            AssertTransmissionHandlerProperties(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, retryStrategy);
        }
        else
        {
            var transmissionHandler = exporterOptions.GetLogsExportTransmissionHandler(new ExperimentalOptions(configuration));

            AssertTransmissionHandlerProperties(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, retryStrategy);
        }
    }

    private static void AssertTransmissionHandlerProperties<T>(OtlpExporterTransmissionHandler<T> transmissionHandler, Type exportClientType, int expectedTimeoutMilliseconds, string retryStrategy)
    {
        if (retryStrategy == "in_memory")
        {
            Assert.True(transmissionHandler is OtlpExporterRetryTransmissionHandler<T>);
        }
        else if (retryStrategy == "disk")
        {
            Assert.True(transmissionHandler is OtlpExporterPersistentStorageTransmissionHandler<T>);
        }
        else
        {
            Assert.True(transmissionHandler is OtlpExporterTransmissionHandler<T>);
        }

        Assert.Equal(exportClientType, transmissionHandler.ExportClient.GetType());

        Assert.Equal(expectedTimeoutMilliseconds, transmissionHandler.TimeoutMilliseconds);
    }
}
