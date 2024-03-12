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

    public static IEnumerable<object[]> GetOtlpExporterOptionsTestCases()
    {
        yield return new object[]
        {
            OtlpExporterOptionsConfigurationType.Default,
            OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName,
            OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName,
            OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName,
            OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName,
            true,
        };

        yield return new object[]
        {
            OtlpExporterOptionsConfigurationType.Logs,
            OtlpSpecConfigDefinitions.LogsEndpointEnvVarName,
            OtlpSpecConfigDefinitions.LogsHeadersEnvVarName,
            OtlpSpecConfigDefinitions.LogsTimeoutEnvVarName,
            OtlpSpecConfigDefinitions.LogsProtocolEnvVarName,
            false,
        };

        yield return new object[]
        {
            OtlpExporterOptionsConfigurationType.Metrics,
            OtlpSpecConfigDefinitions.MetricsEndpointEnvVarName,
            OtlpSpecConfigDefinitions.MetricsHeadersEnvVarName,
            OtlpSpecConfigDefinitions.MetricsTimeoutEnvVarName,
            OtlpSpecConfigDefinitions.MetricsProtocolEnvVarName,
            false,
        };

        yield return new object[]
        {
            OtlpExporterOptionsConfigurationType.Traces,
            OtlpSpecConfigDefinitions.TracesEndpointEnvVarName,
            OtlpSpecConfigDefinitions.TracesHeadersEnvVarName,
            OtlpSpecConfigDefinitions.TracesTimeoutEnvVarName,
            OtlpSpecConfigDefinitions.TracesProtocolEnvVarName,
            false,
        };
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

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
        Assert.Null(options.Headers);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_DefaultsForHttpProtobuf()
    {
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
        Assert.Null(options.Headers);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
    }

    [Theory]
    [MemberData(nameof(GetOtlpExporterOptionsTestCases))]
    public void OtlpExporterOptions_EnvironmentVariableOverride(
        int configurationType,
        string endpointEnvVarKeyName,
        string headersEnvVarKeyName,
        string timeoutEnvVarKeyName,
        string protocolEnvVarKeyName,
        bool appendSignalPathToEndpoint)
    {
        Environment.SetEnvironmentVariable(endpointEnvVarKeyName, "http://test:8888");
        Environment.SetEnvironmentVariable(headersEnvVarKeyName, "A=2,B=3");
        Environment.SetEnvironmentVariable(timeoutEnvVarKeyName, "2000");
        Environment.SetEnvironmentVariable(protocolEnvVarKeyName, "http/protobuf");

        var options = new OtlpExporterOptions((OtlpExporterOptionsConfigurationType)configurationType);

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.Equal(appendSignalPathToEndpoint, options.AppendSignalPathToEndpoint);
    }

    [Theory]
    [MemberData(nameof(GetOtlpExporterOptionsTestCases))]
    public void OtlpExporterOptions_UsingIConfiguration(
        int configurationType,
        string endpointEnvVarKeyName,
        string headersEnvVarKeyName,
        string timeoutEnvVarKeyName,
        string protocolEnvVarKeyName,
        bool appendSignalPathToEndpoint)
    {
        var values = new Dictionary<string, string>()
        {
            [endpointEnvVarKeyName] = "http://test:8888",
            [headersEnvVarKeyName] = "A=2,B=3",
            [timeoutEnvVarKeyName] = "2000",
            [protocolEnvVarKeyName] = "http/protobuf",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, (OtlpExporterOptionsConfigurationType)configurationType, new());

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.Equal(appendSignalPathToEndpoint, options.AppendSignalPathToEndpoint);
    }

    [Fact]
    public void OtlpExporterOptions_InvalidEnvironmentVariableOverride()
    {
        var values = new Dictionary<string, string>()
        {
            ["EndpointWithInvalidValue"] = "invalid",
            ["TimeoutWithInvalidValue"] = "invalid",
            ["ProtocolWithInvalidValue"] = "invalid",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions();

        options.ApplyConfigurationUsingSpecificationEnvVars(
            configuration,
            "EndpointWithInvalidValue",
            appendSignalPathToEndpoint: true,
            "ProtocolWithInvalidValue",
            "NoopHeaders",
            "TimeoutWithInvalidValue");

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);
        Assert.Null(options.Headers);
    }

    [Fact]
    public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
    {
        var values = new Dictionary<string, string>()
        {
            ["Endpoint"] = "http://test:8888",
            ["Timeout"] = "2000",
            ["Protocol"] = "grpc",
            ["Headers"] = "A=2,B=3",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions();

        options.ApplyConfigurationUsingSpecificationEnvVars(
            configuration,
            "Endpoint",
            appendSignalPathToEndpoint: true,
            "Protocol",
            "Headers",
            "Timeout");

        options.Endpoint = new Uri("http://localhost:200");
        options.Headers = "C=3";
        options.TimeoutMilliseconds = 40000;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        Assert.Equal(new Uri("http://localhost:200"), options.Endpoint);
        Assert.Equal("C=3", options.Headers);
        Assert.Equal(40000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.False(options.AppendSignalPathToEndpoint);
    }

    [Fact]
    public void OtlpExporterOptions_EndpointGetterUsesProtocolWhenNull()
    {
        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);

        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);

        options.Protocol = OtlpExportProtocol.Grpc;

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
    }

    [Fact]
    public void OtlpExporterOptions_EndpointThrowsWhenSetToNull()
    {
        var options = new OtlpExporterOptions { Endpoint = new Uri("http://test:8888"), Protocol = OtlpExportProtocol.Grpc };

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, options.Protocol);
    }

    [Fact]
    public void OtlpExporterOptions_EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS", OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TIMEOUT", OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName);
        Assert.Equal("OTEL_EXPORTER_OTLP_PROTOCOL", OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName);
    }

    [Fact]
    public void OtlpExporterOptions_SettingEndpointToNullResetsAppendSignalPathToEndpoint()
    {
        var options = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default);

        Assert.Throws<ArgumentNullException>(() => options.Endpoint = null);
    }

    [Fact]
    public void OtlpExporterOptions_HttpClientFactoryThrowsWhenSetToNull()
    {
        var options = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default);

        Assert.Throws<ArgumentNullException>(() => options.HttpClientFactory = null);
    }

    [Fact]
    public void OtlpExporterOptions_ApplyDefaultsTest()
    {
        var defaultOptions = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default);

        defaultOptions.Endpoint = new Uri("http://default_endpoint/");
        defaultOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
        defaultOptions.Headers = "key1=value1";
        defaultOptions.TimeoutMilliseconds = 18;
        defaultOptions.HttpClientFactory = () => null!;

        var signalOptionsWithoutData = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Traces);

        signalOptionsWithoutData.ApplyDefaults(defaultOptions);

        Assert.Equal(defaultOptions.Endpoint, signalOptionsWithoutData.Endpoint);
        Assert.True(signalOptionsWithoutData.AppendSignalPathToEndpoint);
        Assert.Equal(defaultOptions.Protocol, signalOptionsWithoutData.Protocol);
        Assert.Equal(defaultOptions.Headers, signalOptionsWithoutData.Headers);
        Assert.Equal(defaultOptions.TimeoutMilliseconds, signalOptionsWithoutData.TimeoutMilliseconds);
        Assert.Equal(defaultOptions.HttpClientFactory, signalOptionsWithoutData.HttpClientFactory);

        var signalOptionsWithData = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Metrics);

        signalOptionsWithData.Endpoint = new Uri("http://metrics_endpoint/");
        signalOptionsWithData.Protocol = OtlpExportProtocol.Grpc;
        signalOptionsWithData.Headers = "key2=value2";
        signalOptionsWithData.TimeoutMilliseconds = 1800;
        signalOptionsWithData.HttpClientFactory = () => throw new NotImplementedException();

        signalOptionsWithData.ApplyDefaults(defaultOptions);

        Assert.NotEqual(defaultOptions.Endpoint, signalOptionsWithData.Endpoint);
        Assert.False(signalOptionsWithData.AppendSignalPathToEndpoint);
        Assert.NotEqual(defaultOptions.Protocol, signalOptionsWithData.Protocol);
        Assert.NotEqual(defaultOptions.Headers, signalOptionsWithData.Headers);
        Assert.NotEqual(defaultOptions.TimeoutMilliseconds, signalOptionsWithData.TimeoutMilliseconds);
        Assert.NotEqual(defaultOptions.HttpClientFactory, signalOptionsWithData.HttpClientFactory);
    }

    private static void ClearEnvVars()
    {
        foreach (var item in GetOtlpExporterOptionsTestCases())
        {
            Environment.SetEnvironmentVariable((string)item[1], null);
            Environment.SetEnvironmentVariable((string)item[2], null);
            Environment.SetEnvironmentVariable((string)item[3], null);
            Environment.SetEnvironmentVariable((string)item[4], null);
        }
    }
}
