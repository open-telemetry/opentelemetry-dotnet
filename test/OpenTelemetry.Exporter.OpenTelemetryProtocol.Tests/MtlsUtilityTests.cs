// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class MtlsUtilityTests : IDisposable
{
    private readonly string tempFolder;
    private readonly string validCertPath;
    private readonly string validKeyPath;
    private readonly string invalidCertPath;
    private readonly string nonExistentPath;

    public MtlsUtilityTests()
    {
        // Create a temporary folder for test certificates
        this.tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.tempFolder);

        // Create paths for test files
        this.validCertPath = Path.Combine(this.tempFolder, "valid-cert.pem");
        this.validKeyPath = Path.Combine(this.tempFolder, "valid-key.pem");
        this.invalidCertPath = Path.Combine(this.tempFolder, "invalid-cert.pem");
        this.nonExistentPath = Path.Combine(this.tempFolder, "non-existent.pem");

        // Generate a test certificate and key
        using var rsa = RSA.Create(2048);
        using var cert = MtlsUtilityTests.GenerateTestCertificate(rsa);

        // Export the certificate and key to PEM files
        File.WriteAllText(this.validCertPath, Convert.ToBase64String(cert.RawData));
        File.WriteAllText(this.validKeyPath, Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));

        // Create an invalid certificate file
        File.WriteAllText(this.invalidCertPath, "This is not a valid certificate");
    }

    [Fact]
    public void LoadCertificateWithValidation_WithValidCertificate_ShouldSucceed()
    {
        // Act
        var certificate = MtlsUtility.LoadCertificateWithValidation(this.validCertPath);

        // Assert
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey == false);
    }

    [Fact]
    public void LoadCertificateWithValidation_WithValidCertificateAndKey_ShouldSucceed()
    {
        // Act
        var certificate = MtlsUtility.LoadCertificateWithValidation(this.validCertPath, this.validKeyPath);

        // Assert
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public void LoadCertificateWithValidation_WithNonExistentCertificate_ShouldThrowFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => MtlsUtility.LoadCertificateWithValidation(this.nonExistentPath));
    }

    [Fact]
    public void LoadCertificateWithValidation_WithNonExistentKey_ShouldThrowFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            MtlsUtility.LoadCertificateWithValidation(this.validCertPath, this.nonExistentPath));
    }

    [Fact]
    public void LoadCertificateWithValidation_WithInvalidCertificate_ShouldThrowCryptographicException()
    {
        // Act & Assert
        Assert.Throws<CryptographicException>(() => MtlsUtility.LoadCertificateWithValidation(this.invalidCertPath));
    }

    [Fact]
    public void ValidateCertificateChain_WithSelfSignedCertificate_ShouldSucceed()
    {
        // Arrange
        var cert = MtlsUtility.LoadCertificateWithValidation(this.validCertPath);

        // Act
        var result = MtlsUtility.ValidateCertificateChain(cert, cert);

        // Assert
        Assert.True(result);
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Directory.Exists(this.tempFolder))
            {
                Directory.Delete(this.tempFolder, true);
            }
        }
    }

    private static X509Certificate2 GenerateTestCertificate(RSA rsa)
    {
        var certRequest = new CertificateRequest(
            new X500DistinguishedName("CN=Test Certificate"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        var now = DateTimeOffset.UtcNow;
        return certRequest.CreateSelfSigned(now, now.AddYears(1));
    }
}
#endif
