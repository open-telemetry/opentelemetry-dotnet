// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class BaseOtlpHttpExportClientTests
{
    [Theory]
    [InlineData(null, null, "http://localhost:4318/signal/path")]
    [InlineData(null, "http://from.otel.exporter.env.var", "http://from.otel.exporter.env.var/signal/path")]
    [InlineData("https://custom.host", null, "https://custom.host")]
    [InlineData("http://custom.host:44318/custom/path", null, "http://custom.host:44318/custom/path")]
    [InlineData("https://custom.host", "http://from.otel.exporter.env.var", "https://custom.host")]
    public void ValidateOtlpHttpExportClientEndpoint(string? optionEndpoint, string? endpointEnvVar, string expectedExporterEndpoint)
    {
        try
        {
            Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName, endpointEnvVar);

            OtlpExporterOptions options = new() { Protocol = OtlpExportProtocol.HttpProtobuf };

            if (optionEndpoint != null)
            {
                options.Endpoint = new Uri(optionEndpoint);
            }

            var exporterClient = new TestOtlpHttpExportClient(options, new HttpClient());
            Assert.Equal(new Uri(expectedExporterEndpoint), exporterClient.Endpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName, null);
        }
    }

    internal class TestOtlpHttpExportClient : BaseOtlpHttpExportClient<string>
    {
        public TestOtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient)
            : base(options, httpClient, "signal/path")
        {
        }

        protected override HttpContent CreateHttpContent(string exportRequest)
        {
            throw new NotImplementedException();
        }
    }
}
