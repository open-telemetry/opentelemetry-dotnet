// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Utility class for mutual TLS (mTLS) certificate operations.
/// </summary>
internal static class MtlsUtility
{
    /// <summary>
    /// Loads a certificate from a PEM file with validation checks.
    /// </summary>
    /// <param name="certificateFilePath">Path to the certificate file.</param>
    /// <returns>The loaded certificate.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when there's insufficient permission to read the file.</exception>
    /// <exception cref="CryptographicException">Thrown when the certificate is invalid.</exception>
    public static X509Certificate2 LoadCertificateWithValidation(string certificateFilePath)
    {
        ArgumentNullException.ThrowIfNull(certificateFilePath);

        if (!File.Exists(certificateFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(certificateFilePath);
            throw new FileNotFoundException("Certificate file not found.", certificateFilePath);
        }

#if NET9_0_OR_GREATER
        try
        {
            var certificate = X509Certificate2.CreateFromPemFile(certificateFilePath);
            ValidateCertificate(certificate);
            return certificate;
        }
        catch (CryptographicException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateInvalid(ex);
            throw;
        }
#else
        try
        {
            var certificate = new X509Certificate2(certificateFilePath);
            ValidateCertificate(certificate);
            return certificate;
        }
        catch (CryptographicException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateInvalid(ex);
            throw;
        }
#endif
    }

    /// <summary>
    /// Loads a certificate with its private key from PEM files with validation checks.
    /// </summary>
    /// <param name="certificateFilePath">Path to the certificate file.</param>
    /// <param name="keyFilePath">Path to the private key file.</param>
    /// <returns>The loaded certificate with private key.</returns>
    /// <exception cref="FileNotFoundException">Thrown when either file is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when there's insufficient permission to read either file.</exception>
    /// <exception cref="CryptographicException">Thrown when the certificate or key is invalid.</exception>
    public static X509Certificate2 LoadCertificateWithValidation(string certificateFilePath, string keyFilePath)
    {
        ArgumentNullException.ThrowIfNull(certificateFilePath);
        ArgumentNullException.ThrowIfNull(keyFilePath);

        if (!File.Exists(certificateFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(certificateFilePath);
            throw new FileNotFoundException("Certificate file not found.", certificateFilePath);
        }

        if (!File.Exists(keyFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(keyFilePath);
            throw new FileNotFoundException("Key file not found.", keyFilePath);
        }

        try
        {
            var certificate = X509Certificate2.CreateFromPemFile(certificateFilePath, keyFilePath);
            ValidateCertificate(certificate);
            return certificate;
        }
        catch (CryptographicException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateInvalid(ex);
            throw;
        }
    }

    /// <summary>
    /// Validates a certificate chain against a trusted root certificate.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <param name="trustedRoot">The trusted root certificate.</param>
    /// <returns>True if the chain is valid, false otherwise.</returns>
    public static bool ValidateCertificateChain(X509Certificate2 certificate, X509Certificate2 trustedRoot)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(trustedRoot);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        bool isValid = chain.Build(certificate);

        if (!isValid)
        {
            foreach (var status in chain.ChainStatus)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateChainValidationFailed(
                    status.StatusInformation);
            }
        }

        return isValid;
    }

    private static void ValidateCertificate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        using var chain = new X509Chain();
        if (!chain.Build(certificate))
        {
            var statusInformation = string.Join(", ", chain.ChainStatus.Select(s => $"{s.StatusInformation} ({s.Status})"));
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateChainValidationFailed(statusInformation);
            throw new InvalidOperationException($"Certificate chain validation failed: {statusInformation}");
        }
    }

    private static void ValidateFilePermissions(string filePath)
    {
        try
        {
            // Check if file exists and is readable
            using (File.OpenRead(filePath))
            {
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionCheckFailed(filePath, ex);
            throw;
        }
    }
}
#endif
