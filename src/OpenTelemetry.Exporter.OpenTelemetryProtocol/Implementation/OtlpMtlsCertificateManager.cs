// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using System.Net.Security;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Manages certificate loading, validation, and security checks for mTLS connections.
/// </summary>
internal static class OtlpMtlsCertificateManager
{
    private const string CaCertificateType = "CA certificate";
    private const string ClientCertificateType = "Client certificate";
    private const string ClientPrivateKeyType = "Client private key";

    /// <summary>
    /// Loads a CA certificate from a PEM file.
    /// </summary>
    /// <param name="caCertificatePath">Path to the CA certificate file.</param>
    /// <param name="enableFilePermissionChecks">Whether to check file permissions.</param>
    /// <returns>The loaded CA certificate.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when file permissions are inadequate.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
    public static X509Certificate2 LoadCaCertificate(
        string caCertificatePath,
        bool enableFilePermissionChecks = true)
    {
        ValidateFileExists(caCertificatePath, CaCertificateType);

        if (enableFilePermissionChecks)
        {
            ValidateFilePermissions(caCertificatePath, CaCertificateType);
        }

        try
        {
            var caCertificate = X509Certificate2.CreateFromPemFile(caCertificatePath);

            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateLoaded(
                CaCertificateType,
                caCertificatePath);

            return caCertificate;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateLoadFailed(
                CaCertificateType,
                caCertificatePath,
                ex.Message);
            throw new InvalidOperationException(
                $"Failed to load CA certificate from '{caCertificatePath}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Loads a client certificate with its private key from PEM files.
    /// </summary>
    /// <param name="clientCertificatePath">Path to the client certificate file.</param>
    /// <param name="clientKeyPath">Path to the client private key file.</param>
    /// <param name="enableFilePermissionChecks">Whether to check file permissions.</param>
    /// <returns>The loaded client certificate with private key.</returns>
    /// <exception cref="FileNotFoundException">Thrown when certificate or key files are not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when file permissions are inadequate.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
    public static X509Certificate2 LoadClientCertificate(
        string clientCertificatePath,
        string clientKeyPath,
        bool enableFilePermissionChecks = true)
    {
        ValidateFileExists(clientCertificatePath, ClientCertificateType);
        ValidateFileExists(clientKeyPath, ClientPrivateKeyType);

        if (enableFilePermissionChecks)
        {
            ValidateFilePermissions(clientCertificatePath, ClientCertificateType);
            ValidateFilePermissions(clientKeyPath, ClientPrivateKeyType);
        }

        try
        {
            var clientCertificate = X509Certificate2.CreateFromPemFile(
                clientCertificatePath,
                clientKeyPath);

            if (!clientCertificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "Client certificate does not have an associated private key.");
            }

            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateLoaded(
                ClientCertificateType,
                clientCertificatePath);

            return clientCertificate;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateLoadFailed(
                ClientCertificateType,
                clientCertificatePath,
                ex.Message);
            throw new InvalidOperationException(
                $"Failed to load client certificate from '{clientCertificatePath}' and key from '{clientKeyPath}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Validates the certificate chain for a given certificate.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <param name="certificateType">Type description for logging (e.g., "Client certificate").</param>
    /// <returns>True if the certificate chain is valid; otherwise, false.</returns>
    public static bool ValidateCertificateChain(
        X509Certificate2 certificate,
        string certificateType)
    {
        return ValidateCertificateChain(certificate, certificateType, null);
    }

    /// <summary>
    /// Validates the certificate chain for a given certificate with optional configuration.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <param name="certificateType">Type description for logging (e.g., "Client certificate").</param>
    /// <param name="configuration">Optional configuration to read environment variables from.</param>
    /// <returns>True if the certificate chain is valid; otherwise, false.</returns>
    public static bool ValidateCertificateChain(
        X509Certificate2 certificate,
        string certificateType,
        IConfiguration? configuration)
    {
        try
        {
            using var chain = new X509Chain();

            // Configure chain policy
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            // Configure RevocationMode from environment variable or use default
            var revocationMode = GetRevocationModeFromConfiguration(configuration);
            chain.ChainPolicy.RevocationMode = revocationMode;

            // Configure RevocationFlag from environment variable or use default
            var revocationFlag = GetRevocationFlagFromConfiguration(configuration);
            chain.ChainPolicy.RevocationFlag = revocationFlag;

            bool isValid = chain.Build(certificate);

            if (!isValid)
            {
                var errors = chain
                    .ChainStatus.Where(status => status.Status != X509ChainStatusFlags.NoError)
                    .Select(status => $"{status.Status}: {status.StatusInformation}")
                    .ToArray();

                OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateChainValidationFailed(
                    certificateType,
                    certificate.Subject,
                    string.Join("; ", errors));

                // Check if certificate is expired - this should throw an exception
                bool isExpired = chain.ChainStatus.Any(status =>
                    status.Status == X509ChainStatusFlags.NotTimeValid ||
                    status.Status == X509ChainStatusFlags.NotTimeNested);

                if (isExpired)
                {
                    throw new InvalidOperationException(
                        $"Certificate chain validation failed for {certificateType}: Certificate is expired. " +
                        $"Errors: {string.Join("; ", errors)}");
                }

                return false;
            }

            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateChainValidated(
                certificateType,
                certificate.Subject);
            return true;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateChainValidationFailed(
                certificateType,
                certificate.Subject,
                ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Creates a server certificate validation callback that uses a custom CA certificate.
    /// </summary>
    /// <param name="caCertificate">The CA certificate to use for validation.</param>
    /// <returns>A validation callback function.</returns>
    public static Func<
        X509Certificate2,
        X509Chain,
        SslPolicyErrors,
        bool> CreateServerCertificateValidationCallback(X509Certificate2 caCertificate)
    {
        return (serverCert, chain, sslPolicyErrors) =>
        {
            try
            {
                // If there are no SSL policy errors, accept the certificate
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                // If the only error is an untrusted root, validate against our CA
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                {
                    // Add our CA certificate to the chain
                    chain.ChainPolicy.ExtraStore.Add(caCertificate);
                    chain.ChainPolicy.VerificationFlags =
                        X509VerificationFlags.AllowUnknownCertificateAuthority;

                    bool isValid = chain.Build(serverCert);

                    if (isValid)
                    {
                        // Verify that the chain terminates with our CA
                        var rootCert = chain.ChainElements[^1].Certificate;
                        if (
                            rootCert.Thumbprint.Equals(
                                caCertificate.Thumbprint,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            OpenTelemetryProtocolExporterEventSource.Log.MtlsServerCertificateValidated(
                                serverCert.Subject);
                            return true;
                        }
                    }
                }

                OpenTelemetryProtocolExporterEventSource.Log.MtlsServerCertificateValidationFailed(
                    serverCert.Subject,
                    sslPolicyErrors.ToString());

                return false;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MtlsServerCertificateValidationFailed(
                    serverCert.Subject,
                    ex.Message);

                return false;
            }
        };
    }

    private static void ValidateFileExists(string filePath, string fileType)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException(
                $"{fileType} path cannot be null or empty.",
                nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateFileNotFound(
                fileType,
                filePath);
            throw new FileNotFoundException($"{fileType} file not found at path: {filePath}");
        }
    }

    private static void ValidateFilePermissions(string filePath, string fileType)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                ValidateWindowsFilePermissions(filePath, fileType);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                ValidateUnixFilePermissions(filePath, fileType);
            }

            // For other platforms, skip permission validation
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionCheckFailed(
                fileType,
                filePath,
                ex.Message);
            throw new UnauthorizedAccessException(
                $"File permission check failed for {fileType} at '{filePath}': {ex.Message}",
                ex);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ValidateWindowsFilePermissions(string filePath, string fileType)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSecurity = fileInfo.GetAccessControl();
        var accessRules = fileSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var currentUser = WindowsIdentity.GetCurrent();
        bool hasReadAccess = false;
        bool hasRestrictedAccess = true;

        foreach (FileSystemAccessRule rule in accessRules)
        {
            var identity = rule.IdentityReference as SecurityIdentifier;

            // Check if current user has read access
            if (identity != null && (
                currentUser.User?.Equals(identity) == true
                || currentUser.Groups?.Contains(identity) == true))
            {
                if (
                    rule.AccessControlType == AccessControlType.Allow
                    && (rule.FileSystemRights & FileSystemRights.ReadData) != 0)
                {
                    hasReadAccess = true;
                }
            }

            // Check for overly permissive access (e.g., Everyone, Users group with write access)
            if (
                rule.AccessControlType == AccessControlType.Allow
                && (
                    rule.FileSystemRights
                    & (FileSystemRights.WriteData | FileSystemRights.FullControl)) != 0)
            {
                var wellKnownSids = new[]
                {
                    WellKnownSidType.WorldSid, // Everyone
                    WellKnownSidType.AuthenticatedUserSid, // Authenticated Users
                    WellKnownSidType.BuiltinUsersSid, // Users
                };

                foreach (var sidType in wellKnownSids)
                {
                    var wellKnownSid = new SecurityIdentifier(sidType, null);
                    if (identity?.Equals(wellKnownSid) == true)
                    {
                        hasRestrictedAccess = false;
                        break;
                    }
                }
            }
        }

        if (!hasReadAccess)
        {
            throw new UnauthorizedAccessException(
                $"Current user does not have read access to {fileType} file.");
        }

        if (!hasRestrictedAccess)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionWarning(
                fileType,
                filePath,
                "File has overly permissive access rights. Consider restricting access to improve security.");
        }
    }

    private static void ValidateUnixFilePermissions(string filePath, string fileType)
    {
        var fileInfo = new FileInfo(filePath);

        // On Unix systems, we can check if the file is readable by the current user
        // by attempting to open it for reading
        try
        {
            using var stream = fileInfo.OpenRead();
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Current user does not have read access to {fileType} file.");
        }

        // For Unix systems, we recommend checking file permissions externally
        // as .NET doesn't provide detailed Unix permission APIs
        OpenTelemetryProtocolExporterEventSource.Log.MtlsFilePermissionWarning(
            fileType,
            filePath,
            "Consider verifying that file permissions are set to 400 (read-only for owner) for enhanced security.");
    }

    /// <summary>
    /// Gets the X509RevocationMode from configuration or returns the default value.
    /// </summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <returns>The configured revocation mode or default (Online).</returns>
    private static X509RevocationMode GetRevocationModeFromConfiguration(IConfiguration? configuration)
    {
        if (configuration == null)
        {
            return X509RevocationMode.Online;
        }

        if (configuration.TryGetStringValue(OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, out var modeString))
        {
            if (Enum.TryParse<X509RevocationMode>(modeString, true, out var mode))
            {
                return mode;
            }

            ((IConfigurationExtensionsLogger)OpenTelemetryProtocolExporterEventSource.Log).LogInvalidConfigurationValue(
                OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName,
                modeString);
        }

        return X509RevocationMode.Online;
    }

    /// <summary>
    /// Gets the X509RevocationFlag from configuration or returns the default value.
    /// </summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <returns>The configured revocation flag or default (ExcludeRoot).</returns>
    private static X509RevocationFlag GetRevocationFlagFromConfiguration(IConfiguration? configuration)
    {
        if (configuration == null)
        {
            return X509RevocationFlag.ExcludeRoot;
        }

        if (configuration.TryGetStringValue(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, out var flagString))
        {
            if (Enum.TryParse<X509RevocationFlag>(flagString, true, out var flag))
            {
                return flag;
            }

            ((IConfigurationExtensionsLogger)OpenTelemetryProtocolExporterEventSource.Log).LogInvalidConfigurationValue(
                OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName,
                flagString);
        }

        return X509RevocationFlag.ExcludeRoot;
    }
}

#endif
