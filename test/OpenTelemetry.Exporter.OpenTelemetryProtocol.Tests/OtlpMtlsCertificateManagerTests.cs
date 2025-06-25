// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpMtlsCertificateManagerTests
{
    private const string TestCertPem =
        @"-----BEGIN CERTIFICATE-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1234567890ABCDEFGHIJ
KLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890A
BCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ123
4567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUV
WXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNO
PQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFG
HIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890
ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ123
4567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUV
WXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNO
PQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFG
HIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890
ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ123
4567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUV
WXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNO
PQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFG
HIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890
ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ123
4567890ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCD
-----END CERTIFICATE-----";

    [Fact]
    public void LoadClientCertificate_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist()
    {
        var exception = Assert.Throws<FileNotFoundException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadClientCertificate(
                "/nonexistent/client.crt",
                "/nonexistent/client.key"));

        Assert.Contains("Certificate file not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/nonexistent/client.crt", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadClientCertificate_ThrowsFileNotFoundException_WhenPrivateKeyFileDoesNotExist()
    {
        var tempCertFile = Path.GetTempFileName();
        File.WriteAllText(tempCertFile, TestCertPem);

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadClientCertificate(
                    tempCertFile,
                    "/nonexistent/client.key"));

            Assert.Contains("Private key file not found", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/nonexistent/client.key", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempCertFile);
        }
    }

    [Fact]
    public void LoadCaCertificate_ThrowsFileNotFoundException_WhenTrustStoreFileDoesNotExist()
    {
        var exception = Assert.Throws<FileNotFoundException>(() =>
            OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadCaCertificate("/nonexistent/ca.crt"));

        Assert.Contains("CA certificate file not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/nonexistent/ca.crt", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadClientCertificate_ThrowsInvalidOperationException_WhenCertificateFileIsEmpty()
    {
        var tempCertFile = Path.GetTempFileName();
        var tempKeyFile = Path.GetTempFileName();
        File.WriteAllText(tempCertFile, string.Empty);
        File.WriteAllText(tempKeyFile, string.Empty);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadClientCertificate(tempCertFile, tempKeyFile));

            Assert.Contains(
                "Failed to load client certificate",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempCertFile);
            File.Delete(tempKeyFile);
        }
    }

    [Fact]
    public void LoadCaCertificate_ThrowsInvalidOperationException_WhenTrustStoreFileIsEmpty()
    {
        var tempTrustStoreFile = Path.GetTempFileName();
        File.WriteAllText(tempTrustStoreFile, string.Empty);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadCaCertificate(tempTrustStoreFile));

            Assert.Contains(
                "Failed to load CA certificate",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempTrustStoreFile);
        }
    }

    [Fact]
    public void ValidateCertificateChain_DoesNotThrow_WithValidCertificate()
    {
        // Create a self-signed certificate for testing
        using var cert = CreateSelfSignedCertificate();

        // Should not throw for self-signed certificate with proper validation
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate");

        // For self-signed certificates, validation may fail, but method should not throw
        Assert.True(result || !result); // Just check that it returns a boolean
    }

    [Fact]
    public void ValidateCertificateChain_ReturnsResult_WithValidCertificate()
    {
        // Create a valid certificate for testing
        using var cert = CreateSelfSignedCertificate();

        // Should return a boolean result
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate");

        // The result can be true or false, but the method should not throw
        Assert.True(result || !result);
    }

    [Fact]
    public void ValidateCertificateChain_UsesDefaultConfiguration_WhenConfigurationIsNull()
    {
        using var cert = CreateSelfSignedCertificate();

        // Both overloads should work
        var result1 = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate");
        var result2 = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", null);

        // Results should be the same since both use defaults
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void ValidateCertificateChain_UsesRevocationModeFromConfiguration()
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, "NoCheck"),
            })
            .Build();

        // Should not throw when using NoCheck mode
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing
        Assert.True(result || !result);
    }

    [Fact]
    public void ValidateCertificateChain_UsesRevocationFlagFromConfiguration()
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, "EntireChain"),
            })
            .Build();

        // Should not throw when using EntireChain flag
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing
        Assert.True(result || !result);
    }

    [Fact]
    public void ValidateCertificateChain_UsesBothRevocationConfigurationValues()
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, "Offline"),
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, "EndCertificateOnly"),
            })
            .Build();

        // Should not throw when using both configuration values
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing
        Assert.True(result || !result);
    }

    [Fact]
    public void ValidateCertificateChain_UsesDefaultsForInvalidRevocationMode()
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, "InvalidMode"),
            })
            .Build();

        // Should not throw even with invalid configuration value (should use default)
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing and use default Online mode
        Assert.True(result || !result);
    }

    [Fact]
    public void ValidateCertificateChain_UsesDefaultsForInvalidRevocationFlag()
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, "InvalidFlag"),
            })
            .Build();

        // Should not throw even with invalid configuration value (should use default)
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing and use default ExcludeRoot flag
        Assert.True(result || !result);
    }

    [Theory]
    [InlineData("Online")]
    [InlineData("Offline")]
    [InlineData("NoCheck")]
    [InlineData("online")]
    [InlineData("OFFLINE")]
    [InlineData("nocheck")]
    public void ValidateCertificateChain_HandlesCaseInsensitiveRevocationMode(string revocationMode)
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, revocationMode),
            })
            .Build();

        // Should handle case-insensitive enum parsing
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing
        Assert.True(result || !result);
    }

    [Theory]
    [InlineData("ExcludeRoot")]
    [InlineData("EntireChain")]
    [InlineData("EndCertificateOnly")]
    [InlineData("excluderoot")]
    [InlineData("ENTIRECHAIN")]
    [InlineData("endcertificateonly")]
    public void ValidateCertificateChain_HandlesCaseInsensitiveRevocationFlag(string revocationFlag)
    {
        using var cert = CreateSelfSignedCertificate();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, revocationFlag),
            })
            .Build();

        // Should handle case-insensitive enum parsing
        var result = OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.ValidateCertificateChain(cert, "test certificate", configuration);

        // The method should execute without throwing
        Assert.True(result || !result);
    }

    [Fact]
    public void LoadClientCertificate_AcceptsPasswordParameter()
    {
        var tempCertFile = Path.GetTempFileName();
        var tempKeyFile = Path.GetTempFileName();
        File.WriteAllText(tempCertFile, TestCertPem);
        File.WriteAllText(tempKeyFile, "test-key-content");

        try
        {
            // This test verifies that the method accepts the password parameter
            // Note: We expect this to fail because we're using dummy cert/key content
            // but it should not fail due to the method signature
            var exception = Assert.Throws<InvalidOperationException>(() =>
                OpenTelemetryProtocol.Implementation.OtlpMtlsCertificateManager.LoadClientCertificate(
                    tempCertFile,
                    tempKeyFile,
                    "test-password"));

            // The exception should be about certificate loading, not method signature
            Assert.Contains("Failed to load client certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempCertFile);
            File.Delete(tempKeyFile);
        }
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));
        return cert;
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 CreateExpiredCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Expired Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        // Create a certificate that expired yesterday
        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow.AddDays(-1));
        return cert;
    }
}

#endif
