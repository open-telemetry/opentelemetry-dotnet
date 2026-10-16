// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("EnvVars")]
public sealed class OtlpExporterOptionsTests : IDisposable
{
    public OtlpExporterOptionsTests()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
    }

    public void Dispose() =>
        OtlpSpecConfigDefinitionTests.ClearEnvVars();

    [Fact]
    public void CopyPreservesUnsetValuesAndEndpointPrecedence()
    {
        var source = new OtlpExporterOptions(new ConfigurationBuilder().Build(), OtlpExporterOptionsConfigurationType.Default, new());
        var copy = source.Copy();
        Assert.Equal(source.HasData, copy.HasData);
        Assert.Null(copy.ExportProcessorTypeValue);
        Assert.Null(copy.BatchExportProcessorOptions.MaxQueueSizeValue);
        var defaults = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4318/common"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            TimeoutMilliseconds = 1234,
            MaxResponseSizeBytes = 4096,
            MaxRequestSizeBytes = 1_000_000,
            Compression = OtlpExportCompression.GZip,
        };
        copy.ApplyDefaults(defaults);
        Assert.Equal(defaults.Endpoint, copy.Endpoint);
        Assert.Equal(defaults.Protocol, copy.Protocol);
        Assert.Equal(defaults.TimeoutMilliseconds, copy.TimeoutMilliseconds);
        Assert.Equal(defaults.MaxResponseSizeBytes, copy.MaxResponseSizeBytes);
        Assert.Equal(defaults.MaxRequestSizeBytes, copy.MaxRequestSizeBytes);
        Assert.Equal(defaults.Compression, copy.Compression);
        Assert.True(copy.AppendSignalPathToEndpoint);
        Assert.NotEqual(defaults.Endpoint, source.Endpoint);
        copy.Endpoint = new Uri("http://localhost:4318/signal");
        Assert.False(copy.Copy().AppendSignalPathToEndpoint);
        Assert.Equal(copy.Endpoint, copy.Copy().ApplyDefaults(defaults).Endpoint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CopyDefaultHttpClientFactoryUsesItsOwnOptions(bool explicitlyAssigned, bool inherited)
    {
        var source = new OtlpExporterOptions { TimeoutMilliseconds = 1000, MaxResponseSizeBytes = 2048 };
        if (explicitlyAssigned)
        {
            source.HttpClientFactory = source.DefaultHttpClientFactory;
        }

        var copy = inherited
            ? new OtlpExporterOptions().Copy().ApplyDefaults(source)
            : source.Copy();
        copy.TimeoutMilliseconds = 2000;
        copy.MaxResponseSizeBytes = 4096;
        Assert.Same(copy.DefaultHttpClientFactory, copy.HttpClientFactory);
        Assert.NotSame(source.DefaultHttpClientFactory, copy.DefaultHttpClientFactory);
        using var client = copy.HttpClientFactory();
        Assert.Equal(TimeSpan.FromMilliseconds(2000), client.Timeout);
        Assert.Equal(4096, client.MaxResponseContentBufferSize);
        Assert.Equal(1000, source.TimeoutMilliseconds);
    }

    [Fact]
    public void CopyIsolatesBatchDefaultsWithoutMakingUnsetValuesExplicit()
    {
        var defaults = new BatchExportActivityProcessorOptions { MaxQueueSize = 4096 };
        var source = new OtlpExporterOptions(new ConfigurationBuilder().Build(), OtlpExporterOptionsConfigurationType.Default, defaults);
        source.BatchExportProcessorOptions.MaxExportBatchSize = 1024;
        var copy = source.Copy();
        defaults.MaxQueueSize = 8192;
        source.BatchExportProcessorOptions.MaxExportBatchSize = 2048;
        Assert.Equal(4096, copy.BatchExportProcessorOptions.MaxQueueSize);
        Assert.Null(copy.BatchExportProcessorOptions.MaxQueueSizeValue);
        Assert.Equal(1024, copy.BatchExportProcessorOptions.MaxExportBatchSizeValue);
        copy.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1;
        Assert.Null(source.BatchExportProcessorOptions.ScheduledDelayMillisecondsValue);
    }

#if NET
    [Fact]
    public void CopyIsolatesTlsOptionsAndDefaultFactory()
    {
        var source = new OtlpExporterOptions
        {
            MtlsOptions = new OtlpMtlsOptions
            {
                CaCertificatePath = "ca.pem",
                ClientCertificatePath = "client.pem",
                ClientKeyPath = "key.pem",
                EnableCertificateChainValidation = false,
            },
        };
        var copy = source.Copy();
        Assert.NotSame(source.MtlsOptions, copy.MtlsOptions);
        Assert.Equal("ca.pem", copy.MtlsOptions!.CaCertificatePath);
        Assert.Equal("client.pem", copy.MtlsOptions.ClientCertificatePath);
        Assert.Equal("key.pem", copy.MtlsOptions.ClientKeyPath);
        Assert.False(copy.MtlsOptions.EnableCertificateChainValidation);
        copy.MtlsOptions.CaCertificatePath = null;
        copy.MtlsOptions.ClientCertificatePath = null;
        copy.MtlsOptions.ClientKeyPath = null;
        using var client = copy.HttpClientFactory();
        Assert.Equal("ca.pem", source.MtlsOptions.CaCertificatePath);
        Assert.Equal("client.pem", source.MtlsOptions.ClientCertificatePath);
    }
#endif

    [Fact]
    public void OtlpExporterOptions_Defaults()
    {
        var options = new OtlpExporterOptions();

#if NETFRAMEWORK
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
            ["CompressionWithInvalidValue"] = "invalid",
            ["EndpointWithInvalidValue"] = "invalid",
            ["ProtocolWithInvalidValue"] = "invalid",
            ["TimeoutWithInvalidValue"] = "invalid",
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
            "TimeoutWithInvalidValue",
            "CompressionWithInvalidValue");

#if NETFRAMEWORK
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultHttpEndpoint), options.Endpoint);
#else
        Assert.Equal(new Uri(OtlpExporterOptions.DefaultGrpcEndpoint), options.Endpoint);
#endif

        Assert.Equal(10000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExporterOptions.DefaultOtlpExportProtocol, options.Protocol);
        Assert.Null(options.Headers);
        Assert.Equal(OtlpExportCompression.None, options.Compression);
    }

    [Fact]
    public void OtlpExporterOptions_SetterOverridesEnvironmentVariable()
    {
        var values = new Dictionary<string, string?>
        {
            ["Compression"] = "GZIP",
            ["Endpoint"] = "http://test:8888",
            ["Headers"] = "A=2,B=3",
            ["Protocol"] = "grpc",
            ["Timeout"] = "2000",
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
            "Timeout",
            "Compression");

        options.Endpoint = new Uri("http://localhost:200");
        options.Headers = "C=3";
        options.TimeoutMilliseconds = 40000;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        Assert.Equal(new Uri("http://localhost:200"), options.Endpoint);
        Assert.Equal("C=3", options.Headers);
        Assert.Equal(40000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.False(options.AppendSignalPathToEndpoint);
        Assert.Equal(OtlpExportCompression.GZip, options.Compression);
    }

    [Theory]
    [InlineData("", OtlpExportCompression.None)]
    [InlineData("foo", OtlpExportCompression.None)]
    [InlineData("gzip", OtlpExportCompression.GZip)]
    [InlineData("GZip", OtlpExportCompression.GZip)]
    [InlineData("GZIP", OtlpExportCompression.GZip)]
    [InlineData("none", OtlpExportCompression.None)]
    [InlineData("None", OtlpExportCompression.None)]
    [InlineData("NONE", OtlpExportCompression.None)]
    public void OtlpExporterOptions_AppliesCompressionFromEnvironment(string value, OtlpExportCompression expected)
    {
        var values = new Dictionary<string, string?>
        {
            ["Compression"] = value,
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
            "Timeout",
            "Compression");

        Assert.Equal(expected, options.Compression);
    }

    [Fact]
    public void OtlpExporterOptions_EndpointGetterUsesProtocolWhenNull()
    {
        var options = new OtlpExporterOptions();

#if NETFRAMEWORK
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
    public void OtlpExporterOptions_DefaultHttpClientCapsResponseContentBuffering()
    {
        using var httpClient = new OtlpExporterOptions(OtlpExporterOptionsConfigurationType.Default).HttpClientFactory();

        const int DefaultMessageSizeLimit = 4 * 1024 * 1024; // 4 MiB
        Assert.Equal(DefaultMessageSizeLimit, httpClient.MaxResponseContentBufferSize);
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
            MaxRequestSizeBytes = 4 * 1024 * 1024,
            MaxResponseSizeBytes = 8 * 1024,
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
        Assert.Equal(defaultOptionsWithData.MaxRequestSizeBytes, targetOptionsWithoutData.MaxRequestSizeBytes);
        Assert.Equal(defaultOptionsWithData.MaxResponseSizeBytes, targetOptionsWithoutData.MaxResponseSizeBytes);

        var targetOptionsWithData = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://metrics_endpoint/"),
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
            Protocol = OtlpExportProtocol.Grpc,
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
            Headers = "key2=value2",
            TimeoutMilliseconds = 1800,
            HttpClientFactory = () => throw new NotImplementedException(),
            MaxRequestSizeBytes = 8 * 1024 * 1024,
            MaxResponseSizeBytes = 16 * 1024,
        };

        Assert.True(targetOptionsWithData.HasData);

        targetOptionsWithData.ApplyDefaults(defaultOptionsWithData);

        Assert.NotEqual(defaultOptionsWithData.Endpoint, targetOptionsWithData.Endpoint);
        Assert.False(targetOptionsWithData.AppendSignalPathToEndpoint);
        Assert.NotEqual(defaultOptionsWithData.Protocol, targetOptionsWithData.Protocol);
        Assert.NotEqual(defaultOptionsWithData.Headers, targetOptionsWithData.Headers);
        Assert.NotEqual(defaultOptionsWithData.TimeoutMilliseconds, targetOptionsWithData.TimeoutMilliseconds);
        Assert.NotEqual(defaultOptionsWithData.HttpClientFactory, targetOptionsWithData.HttpClientFactory);
        Assert.NotEqual(defaultOptionsWithData.MaxRequestSizeBytes, targetOptionsWithData.MaxRequestSizeBytes);
        Assert.NotEqual(defaultOptionsWithData.MaxResponseSizeBytes, targetOptionsWithData.MaxResponseSizeBytes);
    }

    [Fact]
    public void MaxRequestSizeBytes_DefaultsToSpecifiedValue()
        => Assert.Equal(67_108_864, new OtlpExporterOptions().MaxRequestSizeBytes);

    [Theory]
    [InlineData(750_000)] // The smallest accepted value: the initial buffer size.
    [InlineData(4 * 1024 * 1024)]
    [InlineData(268_435_456)] // The largest payload the serializer can describe.
    public void MaxRequestSizeBytes_AcceptsValuesWithinRange(int value)
        => Assert.Equal(value, new OtlpExporterOptions { MaxRequestSizeBytes = value }.MaxRequestSizeBytes);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(749_999)]
    [InlineData(268_435_457)]
    [InlineData(int.MaxValue)]
    public void MaxRequestSizeBytes_RejectsValuesOutsideRange(int value)
    {
        var options = new OtlpExporterOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRequestSizeBytes = value);
    }

    [Fact]
    public void MaxResponseSizeBytes_DefaultsToTheSpecifiedValue()
        => Assert.Equal(4_194_304, new OtlpExporterOptions().MaxResponseSizeBytes);

    [Theory]
    [InlineData(1024)] // The smallest accepted value.
    [InlineData(4 * 1024 * 1024)]
    [InlineData(int.MaxValue)]
    public void MaxResponseSizeBytes_AcceptsValuesWithinRange(int value)
        => Assert.Equal(value, new OtlpExporterOptions { MaxResponseSizeBytes = value }.MaxResponseSizeBytes);

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1023)]
    public void MaxResponseSizeBytes_RejectsValuesOutsideRange(int value)
    {
        var options = new OtlpExporterOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxResponseSizeBytes = value);
    }

    [Fact]
    public void MaxResponseSizeBytes_IsAppliedToTheDefaultHttpClient()
    {
        var options = new OtlpExporterOptions { MaxResponseSizeBytes = 12_345 };

        using var client = options.DefaultHttpClientFactory();

        Assert.Equal(12_345, client.MaxResponseContentBufferSize);
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
