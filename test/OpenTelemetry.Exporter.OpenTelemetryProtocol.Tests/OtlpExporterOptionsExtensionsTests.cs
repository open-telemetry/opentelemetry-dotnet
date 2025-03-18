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
    [InlineData("key1=value1")]
    [InlineData("key1=value1,key2=value2")]
    [InlineData("key1=value1,key2=value2,key3=value3")]
    public void GetHeaders_ValidOptionHeaders_ReturnsMergedHeaders(string optionHeaders)
    {
        // Arrange: Create OtlpExporterOptions with specified headers
        var options = new OtlpExporterOptions
        {
            Headers = optionHeaders,
        };

        // Act: Retrieve headers using GetHeaders method
        var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

        // Assert: Verify the count of headers matches the sum of standard headers and provided headers
        Assert.Equal(OtlpExporterOptions.StandardHeaders.Length + optionHeaders.Split(',').Length, headers.Count);

        // Assert: Verify each provided header is present in the result
        foreach (var header in optionHeaders.Split(','))
        {
            var parts = header.Split('=');
            Assert.Contains(headers, entry => entry.Key == parts[0] && entry.Value == parts[1]);
        }

        // Assert: Verify each standard header is present in the result
        foreach (var standardHeader in OtlpExporterOptions.StandardHeaders)
        {
            Assert.Contains(headers, entry => entry.Key == standardHeader.Key && entry.Value == standardHeader.Value);
        }
    }

    [Theory]
    [InlineData("key1=value1,key2=value2,key3=value3")]
    [InlineData("key1=value1,key2=value2,key3=value3,key4=value4")]
    public void GetHeaders_ValidOptionHeadersWithStandardHeaders_ReturnsMergedHeadersWithoutDuplicates(string optionHeaders)
    {
        this.VerifyHeaders(optionHeaders);
    }

    [Theory]
    [InlineData("key1=value1,key2")]
    [InlineData("key1")]
    public void GetHeaders_InvalidOptionHeaders_ThrowsArgumentException(string optionHeaders)
    {
        // Arrange: Create OtlpExporterOptions with invalid headers
        var options = new OtlpExporterOptions
        {
            Headers = optionHeaders,
        };

        // Act & Assert: Verify that an ArgumentException is thrown when calling GetHeaders
        Assert.Throws<ArgumentException>(() => options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v)));
    }

    [Theory]
    [InlineData("key1=value1%2Ckey2=value2", 2)]
    [InlineData("key1=value1%2Ckey2=value2%2Ckey3=value3", 3)]
    public void GetHeaders_UrlEncodedOptionHeaders_ReturnsDecodedHeaders(string optionHeaders, int expectedCount)
    {
        // Unescape the headers before validation
        this.VerifyHeaders(optionHeaders, Uri.UnescapeDataString);
    }

    [Theory]
#if NET462_OR_GREATER
    [InlineData(OtlpExportProtocol.Grpc, typeof(GrpcExportClient))]
#else
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient))]
#endif
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
#if NET462_OR_GREATER
    [InlineData(OtlpExportProtocol.Grpc, typeof(GrpcExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(GrpcExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(GrpcExportClient), false, 10000, "disk")]
#else
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "disk")]
#endif
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
    /// Helper method that verifies the merged headers by:
    /// Optionally preprocessing the input header string (e.g., unescaping URL-encoded headers).
    /// Creating an instance of OtlpExporterOptions with the provided headers.
    /// Retrieving the headers using the GetHeaders method.
    /// Asserting that the total number of headers equals the sum of standard headers and the number of provided headers.
    /// Ensuring each provided header (key-value pair) and each standard header exists in the result.
    /// </summary>
    /// <param name="optionHeaders">The input header string, potentially URL-encoded, to be processed and verified.</param>
    /// <param name="preprocess">Optional function to preprocess the <paramref name="optionHeaders"/>, such as unescaping URL-encoded strings.</param>
    private void VerifyHeaders(string optionHeaders, Func<string, string>? preprocess = null)
    {
        // Preprocess headers if needed (e.g., unescape URL-encoded strings)
        var processedHeaders = preprocess != null ? preprocess(optionHeaders) : optionHeaders;

        // Arrange: Create OtlpExporterOptions with specified headers
        var options = new OtlpExporterOptions { Headers = optionHeaders };

        // Act: Retrieve headers using GetHeaders method
        var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

        // Assert: Verify the total count of headers equals standard headers plus provided headers
        Assert.Equal(OtlpExporterOptions.StandardHeaders.Length + processedHeaders.Split(',').Length, headers.Count);

        // Assert: Verify each provided header is present in the result
        foreach (var header in processedHeaders.Split(','))
        {
            var parts = header.Split('=');
            Assert.Contains(headers, entry => entry.Key == parts[0] && entry.Value == parts[1]);
        }

        // Assert: Verify each standard header is present in the result
        foreach (var standardHeader in OtlpExporterOptions.StandardHeaders)
        {
            Assert.Contains(headers, entry => entry.Key == standardHeader.Key && entry.Value == standardHeader.Value);
        }
    }
}
