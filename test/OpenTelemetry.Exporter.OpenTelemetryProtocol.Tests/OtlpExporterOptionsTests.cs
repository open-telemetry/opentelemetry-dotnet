// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("EnvVars")]
public class OtlpExporterOptionsTests : IDisposable
{
    public OtlpExporterOptionsTests()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
    }

    public void Dispose()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
    }

    [Fact]
    public void OtlpExporterOptions_Defaults()
    {
        var options = new OtlpExporterOptions();

#if NET462_OR_GREATER || NETSTANDARD2_0
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
#else
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
#endif

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
    [ClassData(typeof(OtlpSpecConfigDefinitionTests))]
    public void OtlpExporterOptions_EnvironmentVariableOverride(object testDataObject)
    {
        var testData = testDataObject as OtlpSpecConfigDefinitionTests.TestData;
        Assert.NotNull(testData);

        testData.SetEnvVars();

        var options = new OtlpExporterOptions(testData.ConfigurationType);

        testData.AssertMatches(options);
    }

    [Theory]
    [ClassData(typeof(OtlpSpecConfigDefinitionTests))]
    public void OtlpExporterOptions_UsingIConfiguration(object testDataObject)
    {
        var testData = testDataObject as OtlpSpecConfigDefinitionTests.TestData;
        Assert.NotNull(testData);

        var configuration = testData.ToConfiguration();

        var options = new OtlpExporterOptions(configuration, testData.ConfigurationType, new());

        testData.AssertMatches(options);
    }

    [Fact]
    public void OtlpExporterOptions_InvalidEnvironmentVariableOverride()
    {
        var values = new Dictionary<string, string?>
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

#if NET462_OR_GREATER || NETSTANDARD2_0
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
#else
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
#endif

        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);
        Assert.Null(options.Headers);
    }

    [Fact]
    public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
    {
        var values = new Dictionary<string, string?>
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

#if NET462_OR_GREATER || NETSTANDARD2_0
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
#else
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
#endif

        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);

        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        options.Protocol = OtlpExportProtocol.Grpc;
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
    }

    [Fact]
    public void OtlpExporterOptions_EndpointThrowsWhenSetToNull()
    {
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        var options = new OtlpExporterOptions { Endpoint = new Uri("http://test:8888"), Protocol = OtlpExportProtocol.Grpc };

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, options.Protocol);
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    }

    [Fact]
    public void OtlpExporterOptions_SettingEndpointToNullResetsAppendSignalPathToEndpoint()
    {
        var options = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default);

        Assert.Throws<ArgumentNullException>(() => options.Endpoint = null!);
    }

    [Fact]
    public void OtlpExporterOptions_HttpClientFactoryThrowsWhenSetToNull()
    {
        var options = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default);

        Assert.Throws<ArgumentNullException>(() => options.HttpClientFactory = null!);
    }

    [Fact]
    public void OtlpExporterOptions_ApplyDefaultsTest()
    {
        var defaultOptionsWithData = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://default_endpoint/"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            Headers = "key1=value1",
            TimeoutMilliseconds = 18,
            HttpClientFactory = () => null!,
        };

        Assert.True(defaultOptionsWithData.HasData);

        var targetOptionsWithoutData = new OtlpExporterOptions();

        Assert.False(targetOptionsWithoutData.HasData);

        targetOptionsWithoutData.ApplyDefaults(defaultOptionsWithData);

        Assert.Equal(defaultOptionsWithData.Endpoint, targetOptionsWithoutData.Endpoint);
        Assert.True(targetOptionsWithoutData.AppendSignalPathToEndpoint);
        Assert.Equal(defaultOptionsWithData.Protocol, targetOptionsWithoutData.Protocol);
        Assert.Equal(defaultOptionsWithData.Headers, targetOptionsWithoutData.Headers);
        Assert.Equal(defaultOptionsWithData.TimeoutMilliseconds, targetOptionsWithoutData.TimeoutMilliseconds);
        Assert.Equal(defaultOptionsWithData.HttpClientFactory, targetOptionsWithoutData.HttpClientFactory);

        var targetOptionsWithData = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://metrics_endpoint/"),
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
            Protocol = OtlpExportProtocol.Grpc,
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
            Headers = "key2=value2",
            TimeoutMilliseconds = 1800,
            HttpClientFactory = () => throw new NotImplementedException(),
        };

        Assert.True(targetOptionsWithData.HasData);

        targetOptionsWithData.ApplyDefaults(defaultOptionsWithData);

        Assert.NotEqual(defaultOptionsWithData.Endpoint, targetOptionsWithData.Endpoint);
        Assert.False(targetOptionsWithData.AppendSignalPathToEndpoint);
        Assert.NotEqual(defaultOptionsWithData.Protocol, targetOptionsWithData.Protocol);
        Assert.NotEqual(defaultOptionsWithData.Headers, targetOptionsWithData.Headers);
        Assert.NotEqual(defaultOptionsWithData.TimeoutMilliseconds, targetOptionsWithData.TimeoutMilliseconds);
        Assert.NotEqual(defaultOptionsWithData.HttpClientFactory, targetOptionsWithData.HttpClientFactory);
    }
}
