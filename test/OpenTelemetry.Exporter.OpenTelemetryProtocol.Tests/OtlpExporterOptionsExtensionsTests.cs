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

        var headers = options.GetHeaders();

        Assert.Equal(OtlpExporterOptions.StandardHeaders.Length, headers.Count);

        for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
        {
            Assert.Contains(headers, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key && entry.Value == OtlpExporterOptions.StandardHeaders[i].Value);
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(",")]
    [InlineData("=value1")]
    [InlineData("key1")]
    public void GetHeaders_InvalidOptionHeaders_ThrowsArgumentException(string inputOptionHeaders)
    {
        VerifyHeaders(inputOptionHeaders, string.Empty, true);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("key1=value1", "key1=value1")]
    [InlineData("key1=value1,key2=value2", "key1=value1,key2=value2")]
    [InlineData("key1=value1,key2=value2,key3=value3", "key1=value1,key2=value2,key3=value3")]
    [InlineData("key1=value1,value2", "key1=value1,value2")]
    [InlineData("key1=value1,value2,key2=value3", "key1=value1,value2,key2=value3")]
    [InlineData(" key1 = value1 , key2=value2 ", "key1=value1,key2=value2")]
    [InlineData("key1= value with spaces ,key2=another value", "key1=value with spaces,key2=another value")]
    [InlineData("key1=", "key1=")]
    [InlineData("key1=value1%2Ckey2=value2", "key1=value1,key2=value2")]
    [InlineData("key1=value1%2Ckey2=value2%2Ckey3=value3", "key1=value1,key2=value2,key3=value3")]
    [InlineData("key1=value1%2Cvalue2", "key1=value1,value2")]
    [InlineData("key1=value1%2Cvalue2%2Ckey2=value3", "key1=value1,value2,key2=value3")]
    [InlineData(",key1=value1,key2=value2,", "key1=value1,key2=value2")]
    [InlineData(",,key1=value1,,key2=value2,,", "key1=value1,key2=value2")]
    public void GetHeaders_ValidAndUrlEncodedHeaders_ReturnsCorrectHeaders(string inputOptionHeaders, string expectedNormalizedOptional)
    {
        VerifyHeaders(inputOptionHeaders, expectedNormalizedOptional);
    }

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient))]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
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
    public void AppendPathIfNotPresent_TracesPath_AppendsCorrectly(string input, string expected)
    {
        var uri = new Uri(input, UriKind.Absolute);

        var resultUri = uri.AppendPathIfNotPresent("v1/traces");

        Assert.Equal(expected, resultUri.AbsoluteUri);
    }

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "disk")]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, "in_memory")]
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

    /// <summary>
    /// Validates whether the `Headers` property in `OtlpExporterOptions` is correctly processed and parsed.
    /// It also verifies that the extracted headers match the expected values and checks for expected exceptions.
    /// </summary>
    /// <param name="inputOptionHeaders">The raw header string assigned to `OtlpExporterOptions`.
    /// The format should be "key1=value1,key2=value2" (comma-separated key-value pairs).</param>
    /// <param name="expectedNormalizedOptional">A string representing expected additional headers.
    /// This will be parsed into a dictionary and compared with the actual extracted headers.</param>
    /// <param name="expectException">If `true`, the method expects `GetHeaders` to throw an `ArgumentException`
    /// when processing `inputOptionHeaders`.</param>
    private static void VerifyHeaders(string inputOptionHeaders, string expectedNormalizedOptional, bool expectException = false)
    {
        var options = new OtlpExporterOptions { Headers = inputOptionHeaders };

        if (expectException)
        {
            Assert.Throws<ArgumentException>(() => options.GetHeaders());
            return;
        }

        var headers = options.GetHeaders();

        var actual = string.Join(",", headers.Select(h => $"{h.Key}={h.Value}"));

        var expected = expectedNormalizedOptional;
        if (expected.Length > 0)
        {
            expected += ',';
        }
        expected += string.Join(",", OtlpExporterOptions.StandardHeaders.Select(h => $"{h.Key}={h.Value}"));

        Assert.Equal(expected, actual);
    }
}
