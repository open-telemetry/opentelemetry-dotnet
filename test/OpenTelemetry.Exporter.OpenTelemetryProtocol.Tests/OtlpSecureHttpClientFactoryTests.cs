// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpSecureHttpClientFactoryTests
{
    [Fact]
    public void CreateHttpClient_ThrowsInvalidOperationException_WhenMtlsIsDisabled()
    {
        var options = new OtlpMtlsOptions(); // Disabled by default

        Assert.Throws<InvalidOperationException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options));
    }

    [Fact]
    public void CreateHttpClient_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist()
    {
        var options = new OtlpMtlsOptions { ClientCertificatePath = "/nonexistent/client.crt" };

        var exception = Assert.Throws<FileNotFoundException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options));

        Assert.Contains("Certificate file not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateHttpClient_ConfiguresClientCertificate_WhenValidCertificateProvided()
    {
        var tempCertFile = Path.GetTempFileName();
        try
        {
            // Create a self-signed certificate for testing
            using var cert = CreateSelfSignedCertificate();
            var certBytes = cert.Export(X509ContentType.Pfx);
            File.WriteAllBytes(tempCertFile, certBytes);

            var options = new OtlpMtlsOptions
            {
                ClientCertificatePath = tempCertFile,
                EnableCertificateChainValidation = false, // Ignore validation for test cert
            };

            using var httpClient = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

            Assert.NotNull(httpClient);

            // Verify the HttpClientHandler has client certificates configured
            var handlerField = typeof(HttpMessageInvoker).GetField(
                "_handler",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (handlerField?.GetValue(httpClient) is HttpClientHandler handler)
            {
                Assert.NotEmpty(handler.ClientCertificates);
            }
        }
        finally
        {
            if (File.Exists(tempCertFile))
            {
                File.Delete(tempCertFile);
            }
        }
    }

    [Fact]
    public void CreateHttpClient_ConfiguresServerCertificateValidation_WhenCaCertificatesProvided()
    {
        RunWithCryptoSupportCheck(() =>
        {
            var tempTrustStoreFile = Path.GetTempFileName();
            try
            {
                // Create a self-signed certificate for testing as CA root
                using var caCert = CreateSelfSignedCertificate();
                File.WriteAllText(tempTrustStoreFile, ExportCertificateWithPrivateKey(caCert));

                var options = new OtlpMtlsOptions
                {
                    CaCertificatePath = tempTrustStoreFile,
                    EnableCertificateChainValidation = false, // Avoid platform-specific chain build differences
                };

                using var httpClient = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

                Assert.NotNull(httpClient);

                // Verify the HttpClientHandler has server certificate validation configured
                var handlerField = typeof(HttpMessageInvoker).GetField(
                    "_handler",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (handlerField?.GetValue(httpClient) is HttpClientHandler handler)
                {
                    Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
                }
            }
            finally
            {
                if (File.Exists(tempTrustStoreFile))
                {
                    File.Delete(tempTrustStoreFile);
                }
            }
        });
    }

    [Fact]
    public void CreateHttpClient_ConfiguresServerValidation_WithCaOnly()
    {
        RunWithCryptoSupportCheck(() =>
        {
            var tempTrustStoreFile = Path.GetTempFileName();

            try
            {
                using var caCertificate = CreateCertificateAuthority();
                File.WriteAllText(tempTrustStoreFile, ExportCertificateWithPrivateKey(caCertificate));

                var options = new OtlpMtlsOptions
                {
                    CaCertificatePath = tempTrustStoreFile,
                    EnableCertificateChainValidation = false, // Avoid platform-specific chain build differences
                };

                using var httpClient = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

                var handlerField = typeof(HttpMessageInvoker).GetField(
                    "_handler",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                Assert.NotNull(handlerField);

                var handler = handlerField.GetValue(httpClient) as HttpClientHandler;

                Assert.NotNull(handler);
                Assert.Empty(handler!.ClientCertificates);
                Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
            }
            finally
            {
                if (File.Exists(tempTrustStoreFile))
                {
                    File.Delete(tempTrustStoreFile);
                }
            }
        });
    }

    [Fact]
    public void CreateHttpClient_InvokesServerValidationCallbackAfterFactoryReturns()
    {
        RunWithCryptoSupportCheck(() =>
        {
            var tempTrustStoreFile = Path.GetTempFileName();
            try
            {
                using var caCertificate = CreateCertificateAuthority();
                File.WriteAllText(tempTrustStoreFile, ExportCertificateWithPrivateKey(caCertificate));

                var options = new OtlpMtlsOptions
                {
                    CaCertificatePath = tempTrustStoreFile,
                    EnableCertificateChainValidation = false,
                };

                using var httpClient = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

                var handlerField = typeof(HttpMessageInvoker).GetField(
                    "_handler",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(handlerField);

                var handler = handlerField.GetValue(httpClient) as HttpClientHandler;
                Assert.NotNull(handler);
                Assert.NotNull(handler!.ServerCertificateCustomValidationCallback);

                using var serverCertificate = CreateServerCertificate(caCertificate);
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                var validationResult = handler.ServerCertificateCustomValidationCallback(
                    new HttpRequestMessage(),
                    serverCertificate,
                    chain,
                    SslPolicyErrors.RemoteCertificateChainErrors);

                Assert.True(validationResult);
            }
            finally
            {
                if (File.Exists(tempTrustStoreFile))
                {
                    File.Delete(tempTrustStoreFile);
                }
            }
        });
    }

    [Fact]
    public void ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors()
    {
        using var caCertificate = CreateCertificateAuthority();
        using var serverCertificate = CreateServerCertificate(caCertificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateServerCertificate(
            serverCertificate,
            chain,
            SslPolicyErrors.None,
            caCertificate);

        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_ReturnsFalse_WhenNameMismatch()
    {
        using var caCertificate = CreateCertificateAuthority();
        using var serverCertificate = CreateServerCertificate(caCertificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateServerCertificate(
            serverCertificate,
            chain,
            SslPolicyErrors.RemoteCertificateNameMismatch,
            caCertificate);

        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_ReturnsTrue_WithProvidedCa()
    {
        using var caCertificate = CreateCertificateAuthority();
        using var serverCertificate = CreateServerCertificate(caCertificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateServerCertificate(
            serverCertificate,
            chain,
            SslPolicyErrors.RemoteCertificateChainErrors,
            caCertificate);

        Assert.True(result);
        Assert.Equal(caCertificate.Thumbprint, chain.ChainElements[^1].Certificate.Thumbprint);
    }

    [Fact]
    public void ValidateServerCertificate_ReturnsFalse_WhenCaDoesNotMatch()
    {
        using var caCertificate = CreateCertificateAuthority();
        using var otherCaCertificate = CreateCertificateAuthority();
        using var serverCertificate = CreateServerCertificate(caCertificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateServerCertificate(
            serverCertificate,
            chain,
            SslPolicyErrors.RemoteCertificateChainErrors,
            otherCaCertificate);

        Assert.False(result);
    }

    [Fact]
    public void ValidateCertificateChain_ReturnsFalseForExpiredCertificate()
    {
        using var expiredCertificate = CreateExpiredCertificate();

        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateCertificateChain(
            expiredCertificate,
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ClientCertificateType);

        Assert.False(result);
    }

    [Fact]
    public void CreateSecureHttpClient_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(null!));

        Assert.Equal("tlsOptions", exception.ParamName);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test CA",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 issuer)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        request.CertificateExtensions.Add(sanBuilder.Build());

        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);

        var cert = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30),
            serialNumber);

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 CreateExpiredCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expired Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow.AddDays(-1));

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
    }

    private static string ExportCertificateWithPrivateKey(X509Certificate2 certificate)
    {
        var builder = new StringBuilder();
        builder.AppendLine(certificate.ExportCertificatePem().Trim());

        using RSA? privateKey = certificate.GetRSAPrivateKey();
        if (privateKey != null)
        {
            var pkcs8Bytes = privateKey.ExportPkcs8PrivateKey();
            var privateKeyPem = PemEncoding.Write("PRIVATE KEY", pkcs8Bytes);
            builder.AppendLine(new string(privateKeyPem).Trim());
        }

        return builder.ToString();
    }

    private static void RunWithCryptoSupportCheck(Action testBody)
    {
        try
        {
            testBody();
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.WriteLine($"Skipping mTLS HttpClient tests: {ex.Message}");
        }
        catch (CryptographicException ex) when (ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skipping mTLS HttpClient tests: {ex.Message}");
        }
    }
}

#endif
