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
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName, "http://test:8888");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName, "A=2,B=3");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName, "2000");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName, "http/protobuf");

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
            [OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName] = "http://test:8888",
            [OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName] = "A=2,B=3",
            [OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName] = "2000",
            [OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName] = "http/protobuf",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, OtlpExporterSignals.None, new());

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_InvalidEnvironmentVariableOverride()
    {
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName, "invalid");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName, "invalid");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName, "invalid");

        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri("http://localhost:4317"), options.Endpoint);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(default, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName, "http://test:8888");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName, "A=2,B=3");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName, "2000");
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName, "grpc");

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
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName, "http://test:8888");

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
        Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS", OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TIMEOUT", OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_PROTOCOL", OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName, null);
    }
}
