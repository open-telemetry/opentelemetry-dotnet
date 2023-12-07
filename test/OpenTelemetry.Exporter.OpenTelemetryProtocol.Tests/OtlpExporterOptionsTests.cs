// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExporterOptionsTests : IDisposable
{
    public OtlpExporterOptionsTests()
    {
        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void OtlpExporterOptions_Defaults()
    {
        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri("http://localhost:4317"), options.Endpoint);
        Assert.Null(options.Headers);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.Grpc, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_DefaultsForHttpProtobuf()
    {
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };
        Assert.Equal(new Uri("http://localhost:4318"), options.Endpoint);
        Assert.Null(options.Headers);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_EnvironmentVariableOverride()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "http://test:8888");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, "A=2,B=3");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "2000");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ProtocolEnvVarName, "http/protobuf");

        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_UsingIConfiguration()
    {
        var values = new Dictionary<string, string>()
        {
            [OtlpExporterOptions.EndpointEnvVarName] = "http://test:8888",
            [OtlpExporterOptions.HeadersEnvVarName] = "A=2,B=3",
            [OtlpExporterOptions.TimeoutEnvVarName] = "2000",
            [OtlpExporterOptions.ProtocolEnvVarName] = "http/protobuf",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, new());

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_InvalidEnvironmentVariableOverride()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "invalid");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "invalid");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ProtocolEnvVarName, "invalid");

        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri("http://localhost:4317"), options.Endpoint);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(default, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "http://test:8888");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, "A=2,B=3");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, "2000");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ProtocolEnvVarName, "grpc");

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:200"),
            Headers = "C=3",
            TimeoutMilliseconds = 40000,
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };

        Assert.Equal(new Uri("http://localhost:200"), options.Endpoint);
        Assert.Equal("C=3", options.Headers);
        Assert.Equal(40000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_ProtocolSetterDoesNotOverrideCustomEndpointFromEnvVariables()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, "http://test:8888");

        var options = new OtlpExporterOptions { Protocol = OtlpExportProtocol.Grpc };

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_ProtocolSetterDoesNotOverrideCustomEndpointFromSetter()
    {
        var options = new OtlpExporterOptions { Endpoint = new Uri("http://test:8888"), Protocol = OtlpExportProtocol.Grpc };

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpExporterOptions.EndpointEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS", OtlpExporterOptions.HeadersEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TIMEOUT", OtlpExporterOptions.TimeoutEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_PROTOCOL", OtlpExporterOptions.ProtocolEnvVarName);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ProtocolEnvVarName, null);
    }
}
