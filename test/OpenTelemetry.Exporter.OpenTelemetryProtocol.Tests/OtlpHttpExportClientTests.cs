// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpHttpExportClientTests
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

            using var httpClient = new HttpClient();
            var exporterClient = new OtlpHttpExportClient(options, httpClient, "signal/path");
            Assert.Equal(new Uri(expectedExporterEndpoint), exporterClient.Endpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName, null);
        }
    }
}
