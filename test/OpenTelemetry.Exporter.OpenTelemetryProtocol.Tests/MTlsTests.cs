// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class MtlsTests : IDisposable
{
    private readonly string tempDir;
    private readonly string caCertPath;
    private readonly string clientCertPath;
    private readonly string clientKeyPath;
    private readonly string invalidCertPath;

    public MtlsTests()
    {
        // Create temporary directory for test certificates
        this.tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.tempDir);

        // Set up paths
        this.caCertPath = Path.Combine(this.tempDir, "ca.pem");
        this.clientCertPath = Path.Combine(this.tempDir, "client.pem");
        this.clientKeyPath = Path.Combine(this.tempDir, "client-key.pem");
        this.invalidCertPath = Path.Combine(this.tempDir, "invalid.pem");

        // Create test certificates
        this.CreateTestCertificates();
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void LoadCertificateWithValidation_ValidCertificates_LoadsSuccessfully()
    {
        // Act & Assert - no exception should be thrown
        using (var caCert = MtlsUtility.LoadCertificateWithValidation(this.caCertPath))
        {
            Assert.NotNull(caCert);
            using (var clientCert = MtlsUtility.LoadCertificateWithValidation(this.clientCertPath, this.clientKeyPath))
            {
                Assert.NotNull(clientCert);
            }
        }
    }

    [Fact]
    public void LoadCertificateWithValidation_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() =>
            MtlsUtility.LoadCertificateWithValidation(Path.Combine(this.tempDir, "nonexistent.pem")));

        Assert.Contains("Certificate file not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadCertificateWithValidation_InvalidCertificate_ThrowsException()
    {
        // Arrange - create an invalid certificate file
        File.WriteAllText(this.invalidCertPath, "This is not a valid certificate");

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
            MtlsUtility.LoadCertificateWithValidation(this.invalidCertPath));
    }

    [Fact]
    public void OtlpExporterOptions_WithValidCertificates_ConfiguresHttpClient()
    {
        // Arrange
        var options = new OtlpExporterOptions
        {
            CertificateFilePath = this.caCertPath,
            ClientCertificateFilePath = this.clientCertPath,
            ClientKeyFilePath = this.clientKeyPath,
        };

        // Act
        HttpClient client = options.HttpClientFactory();

        // Assert
        Assert.NotNull(client);

        // Note: We can't directly check the certificates in the handler, but at least we confirm no exception is thrown
    }

    [Fact]
    public void ValidateCertificateChain_ValidChain_ReturnsTrue()
    {
        // Arrange
        using var caCert = MtlsUtility.LoadCertificateWithValidation(this.caCertPath);
        using var clientCert = MtlsUtility.LoadCertificateWithValidation(this.clientCertPath);

        // Act
        bool isValid = MtlsUtility.ValidateCertificateChain(clientCert, caCert);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ConfigureHttpClientForMtls_WithValidCertificates_CreatesClient()
    {
        // Arrange
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("https://localhost:4317"),
            CertificateFilePath = this.caCertPath,
            ClientCertificateFilePath = this.clientCertPath,
            ClientKeyFilePath = this.clientKeyPath,
        };

        // Act
        var client = options.HttpClientFactory();

        // Assert
        Assert.NotNull(client);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Directory.Exists(this.tempDir))
            {
                Directory.Delete(this.tempDir, true);
            }
        }
    }

    private static string PemEncodeX509Certificate(X509Certificate2 cert)
    {
        string pemEncodedCert = "-----BEGIN CERTIFICATE-----\n";
        pemEncodedCert += Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks);
        pemEncodedCert += "\n-----END CERTIFICATE-----";
        return pemEncodedCert;
    }

    private static string PemEncodePrivateKey(RSA rsa)
    {
        var privateKey = rsa.ExportPkcs8PrivateKey();
        string pemEncodedKey = "-----BEGIN PRIVATE KEY-----\n";
        pemEncodedKey += Convert.ToBase64String(privateKey, Base64FormattingOptions.InsertLineBreaks);
        pemEncodedKey += "\n-----END PRIVATE KEY-----";
        return pemEncodedKey;
    }

    private static void MakeFileSecure(string filePath)
    {
        // For security in tests, we'll just ensure the file is readable
        using (File.OpenRead(filePath))
        {
            // Just verify we can read the file
        }
    }

    private void CreateTestCertificates()
    {
        // Generate a simple self-signed certificate for testing
        using var rsa = RSA.Create(2048);
        var distinguishedName = new X500DistinguishedName("CN=Test CA");
        var certRequest = new CertificateRequest(
            distinguishedName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));

        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign,
                false));

        // Create CA certificate
        var caCert = certRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        // Create client certificate signed by the CA
        using var clientKeyRsa = RSA.Create(2048);
        var clientDistinguishedName = new X500DistinguishedName("CN=Test Client");
        var clientCertRequest = new CertificateRequest(
            clientDistinguishedName,
            clientKeyRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        clientCertRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        clientCertRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        var clientCert = clientCertRequest.Create(caCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), new byte[] { 1, 2, 3, 4 });

        // Export certificates and keys to files
        File.WriteAllText(this.caCertPath, PemEncodeX509Certificate(caCert));
        File.WriteAllText(this.clientCertPath, PemEncodeX509Certificate(clientCert));
        File.WriteAllText(this.clientKeyPath, PemEncodePrivateKey(clientKeyRsa));

        // Make files secure
        MakeFileSecure(this.caCertPath);
        MakeFileSecure(this.clientCertPath);
        MakeFileSecure(this.clientKeyPath);
    }
}
#endif
