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

using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        using var defaultHandler = options.GetDefaultHttpMessageHandler();

        Assert.Empty(defaultHandler.ClientCertificates);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void OtlpExporterOptions_WithCACertificate_AddsToHttpMessageHandlerTrustStore()
    {
        var caPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "TrustStore", "rootCA.crt");
        using var serverCert = X509Certificate2.CreateFromPemFile(
            certPemFilePath: Path.Combine(AppContext.BaseDirectory, "Certificates", "TrustStore", "server.crt"),
            keyPemFilePath: Path.Combine(AppContext.BaseDirectory, "Certificates", "TrustStore", "server.key"));

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
        using var defaultHandler = options.GetDefaultHttpMessageHandler();

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;

        var serverCertValidationResult = defaultHandler.ServerCertificateCustomValidationCallback.Invoke(
            new HttpRequestMessage(), serverCert, chain, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(serverCertValidationResult);
    }
#endif

    [Theory]
    [InlineData("4096b-rsa-example-cert.pem", "4096b-rsa-example-keypair.pem", "rsa")]
#if NET5_0_OR_GREATER
    [InlineData("prime256v1-ecdsa-cert.pem", "prime256v1-ecdsa-keypair.pem", "ecdsa")]
#endif
    public void OtlpExporterOptions_WithClientCertificate_PassesCertificateToDefaultHttpClient(string certFileName, string keyFileName, string alg)
    {
        var certPath = Path.Combine(AppContext.BaseDirectory, "Certificates", certFileName);
        var pKeyPath = Path.Combine(AppContext.BaseDirectory, "Certificates", keyFileName);

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
        using var defaultHandler = options.GetDefaultHttpMessageHandler();

        Assert.Single(defaultHandler.ClientCertificates);

        switch (alg)
        {
            case "rsa":
                AssertRsaClientCertificate(defaultHandler.ClientCertificates[0]);
                break;
            case "ecdsa":
                AssertECCCertificate(defaultHandler.ClientCertificates[0]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(alg));
        }
    }

    private static void AssertRsaClientCertificate(X509Certificate certificate)
    {
        // compare thumbprint
        var cert = (X509Certificate2)certificate;
        Assert.Equal("2013BADFCD6BDD058E39B98D6B1177E870603B93", cert.GetCertHashString());
#if NET5_0_OR_GREATER
        var rsa = cert.GetRSAPrivateKey();
#else
        var rsa = (RSA)cert.PrivateKey;
#endif
        Assert.Equal(4096, rsa.KeySize);
    }

    private static void AssertECCCertificate(X509Certificate certificate)
    {
        var cert = (X509Certificate2)certificate;
        Assert.Equal("A94CD0470C3733C084BC43E511EF0AC8DE7898A8", cert.Thumbprint);
    }

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
