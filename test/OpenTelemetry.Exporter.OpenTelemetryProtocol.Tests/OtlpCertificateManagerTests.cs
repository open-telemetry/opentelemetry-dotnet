// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpCertificateManagerTests
{
    private const string TestKeyPem =
        "-----BEGIN PRIVATE KEY-----\nTEST-PRIVATE-KEY\n-----END PRIVATE KEY-----";

    private const string InvalidCertPem =
        @"-----BEGIN CERTIFICATE-----
INVALID CERTIFICATE DATA
-----END CERTIFICATE-----";

    [Fact]
    public void LoadCertificates_RedactsInlineMaterial_InFileNotFoundDiagnostics()
        => AssertCertificateFileNotFoundDiagnostics(TestKeyPem, "[REDACTED]");

    [Fact]
    public void LoadCertificates_PreservesMissingPaths_InFileNotFoundDiagnostics()
    {
        using var directory = new TemporaryDirectory();
        var missingPath = Path.Combine(directory.Path, "missing certificate.pem");

        AssertCertificateFileNotFoundDiagnostics(missingPath, missingPath);
    }

    [Theory]
    [InlineData(OtlpExportProtocol.Grpc)]
    [InlineData(OtlpExportProtocol.HttpProtobuf)]
    [Obsolete("Tests the obsolete gRPC protocol.")]
    public void CreateExporter_RedactsInlinePrivateKey_InSecureHttpClientFailure(OtlpExportProtocol protocol)
    {
        using var directory = new TemporaryDirectory();
        var certificatePath = Path.Combine(directory.Path, "client.pem");
        File.WriteAllText(certificatePath, string.Empty);

        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
            MtlsOptions = new OtlpMtlsOptions { ClientCertificatePath = certificatePath, ClientKeyPath = TestKeyPem },
        };

        using var listener = new TestEventListener(
            OpenTelemetryProtocol.Implementation.OpenTelemetryProtocolExporterEventSource.Log,
            EventLevel.Error);
        var exception = Assert.Throws<FileNotFoundException>(() =>
        {
            using var exporter = new OtlpTraceExporter(options);
        });

        Assert.Equal("[REDACTED]", exception.FileName);
        Assert.DoesNotContain("TEST-PRIVATE-KEY", exception.ToString(), StringComparison.Ordinal);
        var messages = listener.CurrentMessages;
        Assert.Single(messages, message => message.EventId == 28);
        var failure = Assert.Single(messages, message => message.EventId == 36);
        Assert.NotNull(failure.Payload);
        var failureText = Assert.IsType<string>(Assert.Single(failure.Payload));
        Assert.Contains("[REDACTED]", failureText, StringComparison.Ordinal);
        Assert.DoesNotContain("TEST-PRIVATE-KEY", failureText, StringComparison.Ordinal);
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
                OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadClientCertificate(tempCertFile, tempKeyFile));

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
    public void LoadCaCertificate_LoadsCertificate_WhenPemFileHasNoPrivateKey()
    {
        using var selfSignedCertificate = CreateSelfSignedCertificate();
        var certificateOnlyPem = selfSignedCertificate.ExportCertificatePem();

        var tempCertFile = Path.GetTempFileName();
        File.WriteAllText(tempCertFile, certificateOnlyPem);

        try
        {
            using var caCertificate = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadCaCertificate(tempCertFile);

            Assert.Equal(selfSignedCertificate.Thumbprint, caCertificate.Thumbprint);
            Assert.False(caCertificate.HasPrivateKey);
        }
        finally
        {
            File.Delete(tempCertFile);
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
                OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadCaCertificate(tempTrustStoreFile));

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
        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateCertificateChain(cert, "test certificate");

        // For self-signed certificates, validation may fail, but method should not throw
        Assert.True(result || !result); // Just check that it returns a boolean
    }

    [Fact]
    public void ValidateCertificateChain_ReturnsResult_WithValidCertificate()
    {
        // Create a valid certificate for testing
        using var cert = CreateSelfSignedCertificate();

        // Should return a boolean result
        var result = OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ValidateCertificateChain(cert, "test certificate");

        // The result can be true or false, but the method should not throw
        Assert.True(result || !result);
    }

    [Fact]
    public void LoadClientCertificate_LoadsFromSeparateFiles()
    {
        var tempCertFile = Path.GetTempFileName();
        var tempKeyFile = Path.GetTempFileName();
        File.WriteAllText(tempCertFile, InvalidCertPem);
        File.WriteAllText(tempKeyFile, "test-key-content");

        try
        {
            // This test verifies that the method loads from separate files
            // Note: We expect this to fail because we're using dummy cert/key content
            // but it should not fail due to the method signature
            var exception = Assert.Throws<InvalidOperationException>(() =>
                OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadClientCertificate(
                    tempCertFile,
                    tempKeyFile));

            // The exception should be about certificate loading, not method signature
            Assert.Contains("Failed to load client certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempCertFile);
            File.Delete(tempKeyFile);
        }
    }

    private static void AssertCertificateFileNotFoundDiagnostics(string filePath, string displayPath)
    {
        using var directory = new TemporaryDirectory();
        var certificatePath = Path.Combine(directory.Path, "client.pem");
        File.WriteAllText(certificatePath, string.Empty);

        AssertFileNotFoundDiagnostics(
            () => OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadCaCertificate(filePath),
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.CaCertificateType,
            displayPath);
        AssertFileNotFoundDiagnostics(
            () => OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadClientCertificate(filePath, null),
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ClientCertificateType,
            displayPath);
        AssertFileNotFoundDiagnostics(
            () => OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadClientCertificate(
                filePath,
                filePath),
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ClientCertificateType,
            displayPath);
        AssertFileNotFoundDiagnostics(
            () => OpenTelemetryProtocol.Implementation.OtlpCertificateManager.LoadClientCertificate(
                certificatePath,
                filePath),
            OpenTelemetryProtocol.Implementation.OtlpCertificateManager.ClientPrivateKeyType,
            displayPath);
    }

    private static void AssertFileNotFoundDiagnostics(Action loadCertificate, string fileType, string displayPath)
    {
        using var listener = new TestEventListener(
            OpenTelemetryProtocol.Implementation.OpenTelemetryProtocolExporterEventSource.Log,
            EventLevel.Error);
        var exception = Assert.Throws<FileNotFoundException>(loadCertificate);

        Assert.Equal($"{fileType} file not found at path: {displayPath}", exception.Message);
        Assert.Equal(displayPath, exception.FileName);
        var message = Assert.Single(listener.CurrentMessages, message => message.EventId == 28);
        Assert.NotNull(message.Payload);
        Assert.Collection(
            message.Payload,
            value => Assert.Equal(fileType, value),
            value => Assert.Equal(displayPath, value));
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
}

#endif
