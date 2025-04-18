// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

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
        // Check if file exists
        if (!File.Exists(certificateFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(certificateFilePath);
            throw new FileNotFoundException($"Certificate file not found: {certificateFilePath}");
        }

        // Check file permissions
        CheckFilePermissions(certificateFilePath);

        // Load the certificate
        try
        {
            var certificate = X509Certificate2.CreateFromPemFile(certificateFilePath);

            // Validate certificate
            if (!IsCertificateValid(certificate))
            {
                OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateValidationFailed("Certificate validation failed");
                throw new CryptographicException("Certificate validation failed");
            }

            return certificate;
        }
        catch (CryptographicException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateInvalid(ex);
            throw;
        }
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
        // Check if files exist
        if (!File.Exists(certificateFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(certificateFilePath);
            throw new FileNotFoundException($"Certificate file not found: {certificateFilePath}");
        }

        if (!File.Exists(keyFilePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(keyFilePath);
            throw new FileNotFoundException($"Private key file not found: {keyFilePath}");
        }

        // Check file permissions for both files
        CheckFilePermissions(certificateFilePath);
        CheckFilePermissions(keyFilePath);

        // Load the certificate with private key
        try
        {
            var certificate = X509Certificate2.CreateFromPemFile(certificateFilePath, keyFilePath);

            // Validate certificate
            if (!IsCertificateValid(certificate))
            {
                OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateValidationFailed("Certificate validation failed");
                throw new CryptographicException("Certificate validation failed");
            }

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

    private static bool IsCertificateValid(X509Certificate2 certificate)
    {
        // Check that certificate is within its validity period
        var now = DateTime.Now;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateValidationFailed(
                $"Certificate is not valid at the current time. Valid from {certificate.NotBefore} to {certificate.NotAfter}");
            return false;
        }

        return true;
    }

    private static void CheckFilePermissions(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CheckWindowsFilePermissions(filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                CheckUnixFilePermissions(filePath);
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionCheckFailed(filePath, ex);
            // Log but don't throw, as this is a security best practice check, not a requirement
        }
    }

    private static void CheckWindowsFilePermissions(string filePath)
    {
        var fileSecurity = File.GetAccessControl(filePath);
        var accessRules = fileSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var currentUser = WindowsIdentity.GetCurrent().User;
        bool hasUserAccess = false;

        foreach (FileSystemAccessRule rule in accessRules)
        {
            if (rule.IdentityReference.Equals(currentUser) &&
                (rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read &&
                rule.AccessControlType == AccessControlType.Allow)
            {
                hasUserAccess = true;
                break;
            }
        }

        if (!hasUserAccess)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionCheckFailed(
                filePath, new UnauthorizedAccessException("Current user does not have read access to the file"));
        }
    }

    private static void CheckUnixFilePermissions(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var permissions = fileInfo.UnixFileMode;

        // Check if permissions are too open (e.g., world readable)
        if ((permissions & UnixFileMode.OtherRead) != 0)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionCheckFailed(
                filePath,
                new UnauthorizedAccessException("Certificate file has permissions that are too permissive. " +
                                                "Consider restricting with 'chmod 600' or similar"));
        }
    }
}
#endif
