// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

namespace OpenTelemetry.Exporter;

/// <summary>
/// Represents TLS configuration options for OTLP exporter.
/// This class handles server certificate trust for scenarios such as self-signed certificates.
/// </summary>
/// <remarks>
/// The <see cref="CaCertificatePath"/> option enables trust of server certificates
/// that are not verified by a third-party certificate authority. This is commonly used
/// when connecting to servers with self-signed certificates.
/// </remarks>
internal class OtlpTlsOptions
{
    /// <summary>
    /// Gets or sets the path to the CA file in PEM format.
    /// </summary>
    /// <remarks>
    /// This corresponds to the OTEL_EXPORTER_OTLP_CERTIFICATE environment variable.
    /// Use this when the server has a self-signed certificate or uses a private CA.
    /// </remarks>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable certificate chain validation.
    /// When enabled, the exporter will validate the certificate chain and reject invalid certificates.
    /// </summary>
    public bool EnableCertificateChainValidation { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether TLS certificate trust is configured.
    /// </summary>
    public virtual bool IsTlsEnabled =>
        !string.IsNullOrWhiteSpace(this.CaCertificatePath);

    /// <summary>
    /// Gets a value indicating whether mTLS (mutual TLS) is configured.
    /// </summary>
    /// <remarks>
    /// Returns true only when client certificates are configured for mutual authentication.
    /// Server certificate trust alone (CaCertificatePath) does not constitute mTLS.
    /// </remarks>
    public virtual bool IsMtlsEnabled => false;
}

#endif
