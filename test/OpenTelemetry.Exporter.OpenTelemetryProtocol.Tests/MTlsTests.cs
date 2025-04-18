// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
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
            tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Set up paths
            caCertPath = Path.Combine(tempDir, "ca.pem");
            clientCertPath = Path.Combine(tempDir, "client.pem");
            clientKeyPath = Path.Combine(tempDir, "client-key.pem");
            invalidCertPath = Path.Combine(tempDir, "invalid.pem");

            // Create test certificates
            CreateTestCertificates();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void LoadCertificateWithValidation_ValidCertificates_LoadsSuccessfully()
        {
            // Act & Assert - no exception should be thrown
            var caCert = MtlsUtility.LoadCertificateWithValidation(caCertPath);
            Assert.NotNull(caCert);

            var clientCert = MtlsUtility.LoadCertificateWithValidation(clientCertPath, clientKeyPath);
            Assert.NotNull(clientCert);
        }

        [Fact]
        public void LoadCertificateWithValidation_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            var ex = Assert.Throws<FileNotFoundException>(() =>
                MtlsUtility.LoadCertificateWithValidation(Path.Combine(tempDir, "nonexistent.pem")));

            Assert.Contains("Certificate file not found", ex.Message);
        }

        [Fact]
        public void LoadCertificateWithValidation_InvalidCertificate_ThrowsInvalidOperationException()
        {
            // Arrange - create an invalid certificate file
            File.WriteAllText(invalidCertPath, "This is not a valid certificate");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                MtlsUtility.LoadCertificateWithValidation(invalidCertPath));

            Assert.Contains("Failed to load certificate", ex.Message);
        }

        [Fact]
        public void OtlpExporterOptions_WithValidCertificates_ConfiguresHttpClient()
        {
            // Arrange
            var options = new OtlpExporterOptions
            {
                CertificateFilePath = caCertPath,
                ClientCertificateFilePath = clientCertPath,
                ClientKeyFilePath = clientKeyPath
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
            var caCert = new X509Certificate2(caCertPath);
            var clientCert = new X509Certificate2(clientCertPath);

            // Act
            bool isValid = MtlsUtility.ValidateCertificateChain(clientCert, caCert);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void CreateSecureChannel_WithValidCertificates_CreatesChannel()
        {
            // Arrange
            var options = new OtlpExporterOptions
            {
                Endpoint = new Uri("https://localhost:4317"),
                CertificateFilePath = caCertPath,
                ClientCertificateFilePath = clientCertPath,
                ClientKeyFilePath = clientKeyPath
            };

            // Act
            var channel = options.CreateSecureChannel();

            // Assert
            Assert.NotNull(channel);
        }

        private void CreateTestCertificates()
        {
            // Generate a simple self-signed certificate for testing
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var certRequest = new CertificateRequest(
                "CN=Test CA",
                rsa,
                System.Security.Cryptography.X509Certificates.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

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
            var clientKeyRsa = System.Security.Cryptography.RSA.Create(2048);
            var clientCertRequest = new CertificateRequest(
                "CN=Test Client",
                clientKeyRsa,
                System.Security.Cryptography.X509Certificates.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            clientCertRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            clientCertRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

            // Create client certificate signed by CA
            byte[] serialNumber = new byte[8];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(serialNumber);
            }

            var clientCert = clientCertRequest.Create(
                caCert,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1),
                serialNumber);

            // Save certificates to files in PEM format
            File.WriteAllText(caCertPath, PemEncodeX509Certificate(caCert));
            File.WriteAllText(clientCertPath, PemEncodeX509Certificate(clientCert));
            File.WriteAllText(clientKeyPath, PemEncodePrivateKey(clientKeyRsa));

            // Set appropriate file permissions
            MakeFileSecure(caCertPath);
            MakeFileSecure(clientCertPath);
            MakeFileSecure(clientKeyPath);
        }

        private static string PemEncodeX509Certificate(X509Certificate2 cert)
        {
            string pemEncodedCert = "-----BEGIN CERTIFICATE-----\n";
            pemEncodedCert += Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks);
            pemEncodedCert += "\n-----END CERTIFICATE-----";
            return pemEncodedCert;
        }

        private static string PemEncodePrivateKey(System.Security.Cryptography.RSA rsa)
        {
            var privateKey = rsa.ExportPkcs8PrivateKey();
            string pemEncodedKey = "-----BEGIN PRIVATE KEY-----\n";
            pemEncodedKey += Convert.ToBase64String(privateKey, Base64FormattingOptions.InsertLineBreaks);
            pemEncodedKey += "\n-----END PRIVATE KEY-----";
            return pemEncodedKey;
        }

        private static void MakeFileSecure(string filePath)
        {
            // On Windows, set appropriate file permissions
            if (OperatingSystem.IsWindows())
            {
                var security = File.GetAccessControl(filePath);
                security.SetAccessRuleProtection(true, false); // Disable inheritance
                File.SetAccessControl(filePath, security);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // On Unix-like systems, we can set permissions via P/Invoke
                // but for test purposes we'll rely on the current permissions
            }
        }
    }
}
#endif
