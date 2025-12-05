// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("EnvVars")]
public sealed class OtlpExporterOptionsTests : IDisposable
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

#if NETFRAMEWORK || NETSTANDARD2_0
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
#if NET
        Assert.NotNull(testDataObject);
#else
        if (testDataObject == null)
        {
            throw new ArgumentNullException(nameof(testDataObject));
        }
#endif
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
#if NET
        Assert.NotNull(testDataObject);
#else
        if (testDataObject == null)
        {
            throw new ArgumentNullException(nameof(testDataObject));
        }
#endif
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

#if NETFRAMEWORK || NETSTANDARD2_0
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

#if NETFRAMEWORK || NETSTANDARD2_0
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

#if NET
    [Fact]
    public void OtlpExporterOptions_MtlsEnvironmentVariables()
    {
        // Test CA certificate environment variable
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CERTIFICATE", "/path/to/ca.crt");

        try
        {
            var options = new OtlpExporterOptions();

            Assert.NotNull(options.MtlsOptions);
            Assert.Equal("/path/to/ca.crt", options.MtlsOptions.CaCertificatePath);
            Assert.True(options.MtlsOptions.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CERTIFICATE", null);
        }
    }

    [Fact]
    public void OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate()
    {
        // Test client certificate and key environment variables
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE", "/path/to/client.crt");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_KEY", "/path/to/client.key");

        try
        {
            var options = new OtlpExporterOptions();

            Assert.NotNull(options.MtlsOptions);
            Assert.Equal("/path/to/client.crt", options.MtlsOptions.ClientCertificatePath);
            Assert.Equal("/path/to/client.key", options.MtlsOptions.ClientKeyPath);
            Assert.True(options.MtlsOptions.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_KEY", null);
        }
    }

    [Fact]
    public void OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates()
    {
        // Test all mTLS environment variables together
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CERTIFICATE", "/path/to/ca.crt");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE", "/path/to/client.crt");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_KEY", "/path/to/client.key");

        try
        {
            var options = new OtlpExporterOptions();

            Assert.NotNull(options.MtlsOptions);
            Assert.Equal("/path/to/ca.crt", options.MtlsOptions.CaCertificatePath);
            Assert.Equal("/path/to/client.crt", options.MtlsOptions.ClientCertificatePath);
            Assert.Equal("/path/to/client.key", options.MtlsOptions.ClientKeyPath);
            Assert.True(options.MtlsOptions.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CERTIFICATE", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_CLIENT_KEY", null);
        }
    }

    [Fact]
    public void OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables()
    {
        // Ensure no mTLS options are set when no environment variables are present
        var options = new OtlpExporterOptions();

        Assert.Null(options.MtlsOptions);
    }

    [Fact]
    public void OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration()
    {
        // Test using IConfiguration instead of environment variables
        var values = new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_CERTIFICATE"] = "/config/path/to/ca.crt",
            ["OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE"] = "/config/path/to/client.crt",
            ["OTEL_EXPORTER_OTLP_CLIENT_KEY"] = "/config/path/to/client.key",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, OtlpExporterOptionsConfigurationType.Default, new());

        Assert.NotNull(options.MtlsOptions);
        Assert.Equal("/config/path/to/ca.crt", options.MtlsOptions.CaCertificatePath);
        Assert.Equal("/config/path/to/client.crt", options.MtlsOptions.ClientCertificatePath);
        Assert.Equal("/config/path/to/client.key", options.MtlsOptions.ClientKeyPath);
        Assert.True(options.MtlsOptions.IsEnabled);
    }
#endif

    [Fact]
    public void UserAgentProductIdentifier_Default_IsEmpty()
    {
        var options = new OtlpExporterOptions();

        Assert.Equal(string.Empty, options.UserAgentProductIdentifier);
    }

    [Fact]
    public void UserAgentProductIdentifier_DefaultUserAgent_ContainsExporterInfo()
    {
        var options = new OtlpExporterOptions();

        var userAgentHeader = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent");

        Assert.NotNull(userAgentHeader.Key);
        Assert.StartsWith("OTel-OTLP-Exporter-Dotnet/", userAgentHeader.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserAgentProductIdentifier_WithProductIdentifier_IsPrepended()
    {
        var options = new OtlpExporterOptions
        {
            UserAgentProductIdentifier = "MyDistribution/1.2.3",
        };

        Assert.Equal("MyDistribution/1.2.3", options.UserAgentProductIdentifier);

        var userAgentHeader = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent");

        Assert.NotNull(userAgentHeader.Key);
        Assert.StartsWith("MyDistribution/1.2.3 OTel-OTLP-Exporter-Dotnet/", userAgentHeader.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserAgentProductIdentifier_UpdatesStandardHeaders()
    {
        var options = new OtlpExporterOptions();

        var initialUserAgent = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent").Value;
        Assert.StartsWith("OTel-OTLP-Exporter-Dotnet/", initialUserAgent, StringComparison.OrdinalIgnoreCase);

        options.UserAgentProductIdentifier = "MyProduct/1.0.0";

        var updatedUserAgent = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent").Value;
        Assert.StartsWith("MyProduct/1.0.0 OTel-OTLP-Exporter-Dotnet/", updatedUserAgent, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(initialUserAgent, updatedUserAgent);
    }

    [Fact]
    public void UserAgentProductIdentifier_Rfc7231Compliance_SpaceSeparatedTokens()
    {
        var options = new OtlpExporterOptions
        {
            UserAgentProductIdentifier = "MyProduct/1.0.0",
        };

        var userAgentHeader = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent").Value;

        // Should have two product tokens separated by a space
        var tokens = userAgentHeader.Split(' ');
        Assert.Equal(2, tokens.Length);
        Assert.Equal("MyProduct/1.0.0", tokens[0]);
        Assert.StartsWith("OTel-OTLP-Exporter-Dotnet/", tokens[1], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void UserAgentProductIdentifier_EmptyOrWhitespace_UsesDefaultUserAgent(string identifier)
    {
        var options = new OtlpExporterOptions
        {
            UserAgentProductIdentifier = identifier,
        };

        var userAgentHeader = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent").Value;

        // Should only contain the default exporter identifier, no leading space
        Assert.StartsWith("OTel-OTLP-Exporter-Dotnet/", userAgentHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("  ", userAgentHeader, StringComparison.OrdinalIgnoreCase); // No double spaces
    }

    [Fact]
    public void UserAgentProductIdentifier_MultipleProducts_CorrectFormat()
    {
        var options = new OtlpExporterOptions
        {
            UserAgentProductIdentifier = "MySDK/2.0.0 MyDistribution/1.0.0",
        };

        var userAgentHeader = options.StandardHeaders.FirstOrDefault(h => h.Key == "User-Agent").Value;

        Assert.StartsWith("MySDK/2.0.0 MyDistribution/1.0.0 OTel-OTLP-Exporter-Dotnet/", userAgentHeader, StringComparison.OrdinalIgnoreCase);
    }
}
