// <copyright file="OtlpExporterOptionsTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Microsoft.Extensions.Configuration;
using Xunit;
#if NET6_0_OR_GREATER
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
#endif

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
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ClientCertificateFileEnvVarName, "/path/to/my/certificate.pem");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ClientKeyFileEnvVarName, "/path/to/my/key.pem");
        Environment.SetEnvironmentVariable(OtlpExporterOptions.CertificateFileEnvVarName, "/path/to/my/ca.pem");

        var options = new OtlpExporterOptions();

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.Equal("/path/to/my/certificate.pem", options.ClientCertificateFile);
        Assert.Equal("/path/to/my/key.pem", options.ClientKeyFile);
        Assert.Equal("/path/to/my/ca.pem", options.CertificateFile);
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
            [OtlpExporterOptions.ClientCertificateFileEnvVarName] = "/path/to/my/certificate.pem",
            [OtlpExporterOptions.ClientKeyFileEnvVarName] = "/path/to/my/key.pem",
            [OtlpExporterOptions.CertificateFileEnvVarName] = "/path/to/my/ca.pem",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, new());

        Assert.Equal(new Uri("http://test:8888"), options.Endpoint);
        Assert.Equal("A=2,B=3", options.Headers);
        Assert.Equal(2000, options.TimeoutMilliseconds);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, options.Protocol);
        Assert.Equal("/path/to/my/certificate.pem", options.ClientCertificateFile);
        Assert.Equal("/path/to/my/key.pem", options.ClientKeyFile);
        Assert.Equal("/path/to/my/ca.pem", options.CertificateFile);
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

    [Theory]
#if NET6_0_OR_GREATER
    [InlineData(OtlpExportProtocol.HttpProtobuf)]
#endif
    [InlineData(OtlpExportProtocol.Grpc)]
    public void OtlpExporterOptions_ClientCertificateDoesNotExist_ThrowsException(OtlpExportProtocol protocol)
    {
        var certPath = Path.Combine(AppContext.BaseDirectory, "no", "such", "cert.pem");
        var pKeyPath = Path.Combine(AppContext.BaseDirectory, "no", "such", "key.pem");

        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
            ClientCertificateFile = certPath,
            ClientKeyFile = pKeyPath,
        };

        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetLogExportClient(options));
        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetMetricsExportClient(options));
        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetTraceExportClient(options));
    }

    [Theory]
#if NET6_0_OR_GREATER
    [InlineData(OtlpExportProtocol.HttpProtobuf)]
#endif
    [InlineData(OtlpExportProtocol.Grpc)]
    public void OtlpExporterOptions_CertificateDoesNotExist_ThrowsException(OtlpExportProtocol protocol)
    {
        var caPath = Path.Combine(AppContext.BaseDirectory, "no", "such", "ca.pem");

        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
            CertificateFile = caPath,
        };

        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetLogExportClient(options));
        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetMetricsExportClient(options));
        Assert.ThrowsAny<IOException>(() => OtlpExporterOptionsExtensions.GetTraceExportClient(options));
    }

#if !NET6_0_OR_GREATER
    [Fact]
    public void OtlpExporterOptions_ClientCertificateWithHttpProtobuf_ThrowsPlatformNotSupportedException()
    {
        var certPath = Path.Combine(AppContext.BaseDirectory, "otel-test-client-cert.pem");
        var pKeyPath = Path.Combine(AppContext.BaseDirectory, "otel-test-client-key.pem");

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            ClientCertificateFile = certPath,
            ClientKeyFile = pKeyPath,
        };

        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetLogExportClient(options));
        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetMetricsExportClient(options));
        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetTraceExportClient(options));
    }

    [Fact]
    public void OtlpExporterOptions_CACertificateWithHttpProtobuf_ThrowsPlatformNotSupportedException()
    {
        var caPath = Path.Combine(AppContext.BaseDirectory, "my", "ca.pem");

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            CertificateFile = caPath,
        };

        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetLogExportClient(options));
        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetMetricsExportClient(options));
        Assert.ThrowsAny<PlatformNotSupportedException>(() => OtlpExporterOptionsExtensions.GetTraceExportClient(options));
    }
#endif

    [Fact]
    public void OtlpExporterOptions_NoCertificate_DefaultHttpClientDoesnotHaveCertificate()
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
        using var defaultHandler = options.CreateDefaultHttpMessageHandler();

        Assert.Empty(defaultHandler.ClientCertificates);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void OtlpExporterOptions_WithCACertificate_AddsToHttpMessageHandlerTrustStore()
    {
        var caPath = Path.Combine(AppContext.BaseDirectory, "otel-test-ca-cert.pem");

        using var serverCert = X509Certificate2.CreateFromPemFile(
            certPemFilePath: Path.Combine(AppContext.BaseDirectory, "otel-test-server-cert.pem"),
            keyPemFilePath: Path.Combine(AppContext.BaseDirectory, "otel-test-server-key.pem"));

        var values = new Dictionary<string, string>()
        {
            [OtlpExporterOptions.EndpointEnvVarName] = "http://test:8888",
            [OtlpExporterOptions.HeadersEnvVarName] = "A=2,B=3",
            [OtlpExporterOptions.TimeoutEnvVarName] = "2000",
            [OtlpExporterOptions.ProtocolEnvVarName] = "http/protobuf",
            [OtlpExporterOptions.CertificateFileEnvVarName] = caPath,
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, new());
        using var defaultHandler = options.CreateDefaultHttpMessageHandler();

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;

        var serverCertValidationResult = defaultHandler.ServerCertificateCustomValidationCallback.Invoke(
            new HttpRequestMessage(), serverCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(serverCertValidationResult);
    }

    [Fact]
    public void OtlpExporterOptions_WithClientCertificate_PassesCertificateToDefaultHttpClient()
    {
        var certPath = Path.Combine(AppContext.BaseDirectory, "otel-test-client-cert.pem");
        var pKeyPath = Path.Combine(AppContext.BaseDirectory, "otel-test-client-key.pem");

        var values = new Dictionary<string, string>()
        {
            [OtlpExporterOptions.EndpointEnvVarName] = "http://test:8888",
            [OtlpExporterOptions.HeadersEnvVarName] = "A=2,B=3",
            [OtlpExporterOptions.TimeoutEnvVarName] = "2000",
            [OtlpExporterOptions.ProtocolEnvVarName] = "http/protobuf",
            [OtlpExporterOptions.ClientCertificateFileEnvVarName] = certPath,
            [OtlpExporterOptions.ClientKeyFileEnvVarName] = pKeyPath,
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new OtlpExporterOptions(configuration, new());
        using var defaultHandler = options.CreateDefaultHttpMessageHandler();

        Assert.Single(defaultHandler.ClientCertificates);
        Assert.Contains("CN=otel-test-client", (defaultHandler.ClientCertificates[0] as X509Certificate2).Subject);
    }
#endif

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.HeadersEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.TimeoutEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ProtocolEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.CertificateFileEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ClientCertificateFileEnvVarName, null);
        Environment.SetEnvironmentVariable(OtlpExporterOptions.ClientKeyFileEnvVarName, null);
    }
}
