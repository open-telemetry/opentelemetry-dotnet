// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class ExporterClientValidationTests : Http2UnencryptedSupportTests
{
    private const string HttpEndpoint = "http://localhost:4173";
    private const string HttpsEndpoint = "https://localhost:4173";

    [Fact]
    public void ExporterClientValidation_FlagIsEnabledForHttpEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpEndpoint),
        };

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));
        Assert.Null(exception);
    }

    [Fact]
    public void ExporterClientValidation_FlagIsNotEnabledForHttpEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpEndpoint),
        };

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", false);

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));

        if (Environment.Version.Major == 3)
        {
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
        }
        else
        {
            Assert.Null(exception);
        }
    }

    [Fact]
    public void ExporterClientValidation_FlagIsNotEnabledForHttpsEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpsEndpoint),
        };

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));
        Assert.Null(exception);
    }
}
