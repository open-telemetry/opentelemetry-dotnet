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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Utility class for handling mTLS (mutual TLS) operations.
/// </summary>
internal static class MTlsUtility
{
    /// <summary>
    /// Loads a certificate from a PEM file with validation.
    /// </summary>
    /// <param name="certificateFilePath">The path to the certificate PEM file.</param>
    /// <param name="keyFilePath">Optional path to the private key PEM file.</param>
    /// <returns>The loaded X509Certificate2.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file is not found.</exception>
    /// <exception cref="SecurityException">Thrown when the certificate file has invalid permissions.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded or is invalid.</exception>
    public static X509Certificate2 LoadCertificateWithValidation(string certificateFilePath, string? keyFilePath = null)
    {
        // Check if certificate file exists
        if (!File.Exists(certificateFilePath))
        {
            throw new FileNotFoundException($"Certificate file not found: {certificateFilePath}");
        }

        // If key file is provided, check if it exists
        if (!string.IsNullOrEmpty(keyFilePath) && !File.Exists(keyFilePath))
        {
            throw new FileNotFoundException($"Private key file not found: {keyFilePath}");
        }

        // Check file permissions
        ValidateFilePermissions(certificateFilePath);
        if (!string.IsNullOrEmpty(keyFilePath))
        {
            ValidateFilePermissions(keyFilePath);
        }

        try
        {
            // Load the certificate
            if (string.IsNullOrEmpty(keyFilePath))
            {
                return X509Certificate2.CreateFromPemFile(certificateFilePath);
            }
            else
            {
                return X509Certificate2.CreateFromPemFile(certificateFilePath, keyFilePath);
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
            throw new InvalidOperationException($"Failed to load certificate: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the chain of a certificate.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <param name="trustedRoot">Optional trusted root certificate.</param>
    /// <returns>True if validation succeeds, false otherwise.</returns>
    public static bool ValidateCertificateChain(X509Certificate2 certificate, X509Certificate2? trustedRoot = null)
    {
        using var chain = new X509Chain();

        if (trustedRoot != null)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
        }

        // Validate the certificate chain
        bool isValid = chain.Build(certificate);

        if (!isValid)
        {
            // Log detailed error information
            foreach (var status in chain.ChainStatus)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateChainValidationError(status.Status.ToString(), status.StatusInformation);
            }
        }

        return isValid;
    }

    /// <summary>
    /// Validates file permissions to ensure they are secure.
    /// </summary>
    /// <param name="filePath">The path to the file to validate.</param>
    /// <exception cref="SecurityException">Thrown when the file has insecure permissions.</exception>
    public static void ValidateFilePermissions(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ValidateWindowsFilePermissions(filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ValidateUnixFilePermissions(filePath);
            }
        }
        catch (Exception ex) when (!(ex is SecurityException))
        {
            // Log but don't fail on permission check errors
            OpenTelemetryProtocolExporterEventSource.Log.MTlsPermissionCheckWarning(filePath, ex);
        }
    }

    private static void ValidateWindowsFilePermissions(string filePath)
    {
        var fileSecurity = File.GetAccessControl(filePath);
        var accessRules = fileSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));

        // Get current user's SID
        var currentUser = WindowsIdentity.GetCurrent().User;

        bool hasUserReadAccess = false;
        bool hasEveryoneReadAccess = false;

        foreach (FileSystemAccessRule rule in accessRules)
        {
            if (rule.IdentityReference is SecurityIdentifier sid)
            {
                // Check if current user has read access
                if (sid.Equals(currentUser) &&
                    (rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read &&
                    rule.AccessControlType == AccessControlType.Allow)
                {
                    hasUserReadAccess = true;
                }

                // Check if Everyone group has access
                if (sid.Equals(new SecurityIdentifier(WellKnownSidType.WorldSid, null)) &&
                    (rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read &&
                    rule.AccessControlType == AccessControlType.Allow)
                {
                    hasEveryoneReadAccess = true;
                }
            }
        }

        // If Everyone has read access, this is insecure
        if (hasEveryoneReadAccess)
        {
            throw new SecurityException($"Insecure file permissions: '{filePath}' is readable by Everyone group");
        }

        // Ensure current user has read access
        if (!hasUserReadAccess)
        {
            throw new SecurityException($"Current user does not have read access to '{filePath}'");
        }
    }

    private static void ValidateUnixFilePermissions(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var permissions = (int)fileInfo.UnixFileMode;

        // Check if file is world-readable (permissions & 4) or world-writable (permissions & 2)
        if ((permissions & 4) != 0 || (permissions & 2) != 0)
        {
            throw new SecurityException($"Insecure file permissions: '{filePath}' has world-readable or world-writable permissions");
        }
    }
}
#endif
