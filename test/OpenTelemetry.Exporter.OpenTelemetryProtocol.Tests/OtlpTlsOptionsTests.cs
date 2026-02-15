// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// Tests for TLS options and secure HTTP client configuration.
/// </summary>
public class OtlpTlsOptionsTests
{
    [Fact]
    public void OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenNoCaCertificatePath()
    {
        var options = new OtlpTlsOptions();
        Assert.False(options.IsTlsEnabled);
    }

    [Fact]
    public void OtlpTlsOptions_IsTlsEnabled_ReturnsTrue_WhenCaCertificatePathProvided()
    {
        var options = new OtlpTlsOptions { CaCertificatePath = "/path/to/ca.crt" };
        Assert.True(options.IsTlsEnabled);
    }

    [Fact]
    public void OtlpTlsOptions_IsMtlsEnabled_ReturnsFalse_ByDefault()
    {
        var options = new OtlpTlsOptions { CaCertificatePath = "/path/to/ca.crt" };
        Assert.False(options.IsMtlsEnabled);
    }

    [Fact]
    public void OtlpMtlsOptions_IsMtlsEnabled_ReturnsTrue_WhenClientCertificateProvided()
    {
        var options = new OtlpMtlsOptions { ClientCertificatePath = "/path/to/client.crt" };
        Assert.True(options.IsMtlsEnabled);
    }

    [Fact]
    public void OtlpMtlsOptions_IsMtlsEnabled_ReturnsFalse_WhenOnlyCaCertificateProvided()
    {
        // This is the key distinction: CA alone does NOT constitute mTLS
        var options = new OtlpMtlsOptions { CaCertificatePath = "/path/to/ca.crt" };
        Assert.False(options.IsMtlsEnabled);
        Assert.True(options.IsTlsEnabled); // But TLS is still enabled for server cert validation
    }

    [Fact]
    public void OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly()
    {
        SkipTestIfCryptoNotSupported(() =>
        {
            var tempCertFile = Path.GetTempFileName();
            try
            {
                using var cert = CreateSelfSignedCertificate();
                File.WriteAllText(tempCertFile, ExportCertificateWithPrivateKey(cert));

                var options = new OtlpTlsOptions
                {
                    CaCertificatePath = tempCertFile,
                    EnableCertificateChainValidation = false,
                };

                using var client = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

                Assert.NotNull(client);
            }
            finally
            {
                if (File.Exists(tempCertFile))
                {
                    File.Delete(tempCertFile);
                }
            }
        });
    }

    [Fact]
    public void OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate()
    {
        SkipTestIfCryptoNotSupported(() =>
        {
            var tempCertFile = Path.GetTempFileName();
            try
            {
                using var cert = CreateSelfSignedCertificate();
                var certBytes = cert.Export(X509ContentType.Pfx);
                File.WriteAllBytes(tempCertFile, certBytes);

                var options = new OtlpMtlsOptions
                {
                    ClientCertificatePath = tempCertFile,
                    EnableCertificateChainValidation = false,
                };

                using var client = OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options);

                Assert.NotNull(client);
            }
            finally
            {
                if (File.Exists(tempCertFile))
                {
                    File.Delete(tempCertFile);
                }
            }
        });
    }

    [Fact]
    public void OtlpSecureHttpClientFactory_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(null!));
    }

    [Fact]
    public void OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled()
    {
        var options = new OtlpTlsOptions();
        Assert.Throws<InvalidOperationException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpSecureHttpClientFactory.CreateSecureHttpClient(options));
    }

    [Fact]
    public void OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadCaCertificate("/nonexistent/cert.pem"));
    }

    [Fact]
    public void OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors()
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
    public void OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WithProvidedTrustedCert()
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

        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
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

        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
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

        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
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

    /// <summary>
    /// Executes a test action and gracefully handles platforms where cryptographic operations are not supported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some platforms (e.g., certain CI environments or restricted OS configurations) may not support
    /// specific cryptographic operations required for TLS/mTLS certificate handling. This method wraps
    /// test execution to catch <see cref="PlatformNotSupportedException"/> and <see cref="CryptographicException"/>
    /// (when indicating lack of support), allowing tests to pass gracefully on unsupported platforms.
    /// </para>
    /// <para>
    /// Note: xUnit 2.x does not support runtime test skipping. The test will appear as "passed" rather than
    /// "skipped" when crypto is not supported. Consider upgrading to xUnit v3 for proper <c>Assert.Skip()</c> support.
    /// </para>
    /// </remarks>
    /// <param name="testBody">The test action to execute.</param>
    private static void SkipTestIfCryptoNotSupported(Action testBody)
    {
        try
        {
            testBody();
        }
        catch (PlatformNotSupportedException ex)
        {
            // Platform does not support the required cryptographic operations.
            // Test is effectively skipped but will appear as passed in xUnit 2.x.
            Console.WriteLine($"[SKIPPED] TLS test skipped due to platform limitation: {ex.Message}");
        }
        catch (CryptographicException ex) when (ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
        {
            // Cryptographic operation not supported on this platform/configuration.
            // Test is effectively skipped but will appear as passed in xUnit 2.x.
            Console.WriteLine($"[SKIPPED] TLS test skipped due to crypto limitation: {ex.Message}");
        }
    }
}

#endif
