// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Manages certificate loading, validation, and security checks for mTLS connections.
/// </summary>
internal static class OtlpMtlsCertificateManager
{
    internal const string CaCertificateType = "CA certificate";
    internal const string ClientCertificateType = "Client certificate";
    internal const string ClientPrivateKeyType = "Client private key";

    /// <summary>
    /// Loads a CA certificate from a PEM file.
    /// </summary>
    /// <param name="caCertificatePath">Path to the CA certificate file.</param>
    /// <returns>The loaded CA certificate.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
    public static X509Certificate2 LoadCaCertificate(string caCertificatePath)
    {
        ValidateFileExists(caCertificatePath, CaCertificateType);

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
    /// <returns>The loaded client certificate with private key.</returns>
    /// <exception cref="FileNotFoundException">Thrown when certificate or key files are not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
    public static X509Certificate2 LoadClientCertificate(
        string clientCertificatePath,
        string clientKeyPath)
    {
        return LoadClientCertificate(clientCertificatePath, clientKeyPath, null);
    }

    /// <summary>
    /// Loads a client certificate from a single file (e.g., PKCS#12 format) with optional password.
    /// </summary>
    /// <param name="clientCertificatePath">Path to the client certificate file.</param>
    /// <param name="clientKeyPath">Must be null for single-file certificates.</param>
    /// <param name="clientKeyPassword">Password for the certificate file if it is encrypted. Can be null for unencrypted certificates.</param>
    /// <returns>The loaded client certificate with private key.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be loaded.</exception>
    /// <exception cref="ArgumentException">Thrown when clientKeyPath is not null for single-file certificate loading.</exception>
    public static X509Certificate2 LoadClientCertificate(
        string clientCertificatePath,
        string? clientKeyPath,
        string? clientKeyPassword)
    {
        if (clientKeyPath == null)
        {
            // Load certificate from a single file (e.g., PKCS#12 format)
            ValidateFileExists(clientCertificatePath, ClientCertificateType);

            try
            {
                X509Certificate2 clientCertificate;

                // Try to load as PKCS#12 first, then as PEM
                if (!string.IsNullOrEmpty(clientKeyPassword))
                {
                    // Load PKCS#12 with password
#if NET9_0_OR_GREATER
                    clientCertificate = X509CertificateLoader.LoadPkcs12FromFile(clientCertificatePath, clientKeyPassword);
#else
#pragma warning disable SYSLIB0057 // X509Certificate2 constructors are obsolete. Use X509CertificateLoader instead.
                    clientCertificate = new X509Certificate2(clientCertificatePath, clientKeyPassword);
#pragma warning restore SYSLIB0057
#endif
                }
                else
                {
                    // Try PKCS#12 without password first
                    try
                    {
#if NET9_0_OR_GREATER
                        clientCertificate = X509CertificateLoader.LoadPkcs12FromFile(clientCertificatePath, (string?)null);
#else
#pragma warning disable SYSLIB0057 // X509Certificate2 constructors are obsolete. Use X509CertificateLoader instead.
                        clientCertificate = new X509Certificate2(clientCertificatePath);
#pragma warning restore SYSLIB0057
#endif
                    }
                    catch
                    {
                        // If PKCS#12 fails, try PEM format
                        clientCertificate = X509Certificate2.CreateFromPemFile(clientCertificatePath);
                    }
                }

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
                    $"Failed to load client certificate from '{clientCertificatePath}': {ex.Message}",
                    ex);
            }
        }

        // Load certificate and key from separate files
        ValidateFileExists(clientCertificatePath, ClientCertificateType);
        ValidateFileExists(clientKeyPath, ClientPrivateKeyType);

        try
        {
            X509Certificate2 clientCertificate;

            // Choose the appropriate method based on whether a password is provided
            if (!string.IsNullOrEmpty(clientKeyPassword))
            {
                clientCertificate = X509Certificate2.CreateFromEncryptedPemFile(
                    clientCertificatePath,
                    clientKeyPath,
                    clientKeyPassword);
            }
            else
            {
                clientCertificate = X509Certificate2.CreateFromPemFile(
                    clientCertificatePath,
                    clientKeyPath);
            }

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
    /// Validates a server certificate against the provided CA certificate.
    /// </summary>
    /// <param name="serverCert">The server certificate to validate.</param>
    /// <param name="chain">The certificate chain.</param>
    /// <param name="sslPolicyErrors">The SSL policy errors.</param>
    /// <param name="caCertificate">The CA certificate to validate against.</param>
    /// <returns>True if the certificate is valid; otherwise, false.</returns>
    internal static bool ValidateServerCertificate(
        X509Certificate2 serverCert,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2 caCertificate)
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
                        string.Equals(
                            rootCert.Thumbprint,
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
            throw new FileNotFoundException($"{fileType} file not found at path: {filePath}", filePath);
        }
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
        if (configuration != null && configuration.TryGetStringValue(OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, out var flagString))
        {
            if (Enum.TryParse<X509RevocationFlag>(flagString, true, out var flag))
            {
                return flag;
            }

            ((IConfigurationExtensionsLogger)OpenTelemetryProtocolExporterEventSource.Log).LogInvalidConfigurationValue(
                OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName,
                flagString);
        }

        // Use ExcludeRoot as default to avoid revocation checks on the root CA certificate,
        // which is typically self-signed and may not have revocation information available
        return X509RevocationFlag.ExcludeRoot;
    }
}

#endif
