// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

namespace OpenTelemetry.Exporter;

internal sealed class OtlpMtlsOptions
{
    /// <summary>
    /// Gets or sets the path to the CA certificate file in PEM format.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the client certificate file in PEM format.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the client private key file in PEM format.
    /// </summary>
    public string? ClientKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the password for the client private key file when it is encrypted.
    /// This is only used when the private key file is password-protected.
    /// If not provided and the key file is encrypted, certificate loading will fail.
    /// </summary>
    public string? ClientKeyPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable certificate chain validation.
    /// When enabled, the exporter will validate the certificate chain and reject invalid certificates.
    /// </summary>
    public bool EnableCertificateChainValidation { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether mTLS is enabled.
    /// mTLS is considered enabled if at least the client certificate path is provided.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(this.ClientCertificatePath);
}

#endif
