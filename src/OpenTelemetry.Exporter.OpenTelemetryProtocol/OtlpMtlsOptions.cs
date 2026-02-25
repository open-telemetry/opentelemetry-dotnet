// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

namespace OpenTelemetry.Exporter;

/// <summary>
/// Represents mTLS (mutual TLS) configuration options for OTLP exporter.
/// Extends <see cref="OtlpTlsOptions"/> with client certificate authentication.
/// </summary>
/// <remarks>
/// mTLS is an authentication system in which both the client and server authenticate each other.
/// This class provides client certificate configuration for scenarios requiring mutual authentication.
/// For simple server certificate trust (e.g., self-signed certificates), use <see cref="OtlpTlsOptions"/> directly.
/// </remarks>
internal sealed class OtlpMtlsOptions : OtlpTlsOptions
{
    /// <summary>
    /// Gets or sets the path to the client certificate file in PEM format.
    /// </summary>
    /// <remarks>
    /// Corresponds to the OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE environment variable.
    /// This is used for client authentication in mTLS scenarios.
    /// </remarks>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the path to the client private key file in PEM format.
    /// </summary>
    /// <remarks>
    /// Corresponds to the OTEL_EXPORTER_OTLP_CLIENT_KEY environment variable.
    /// Required when the client certificate file does not include the private key.
    /// </remarks>
    public string? ClientKeyPath { get; set; }

    /// <summary>
    /// Gets a value indicating whether mTLS (mutual TLS) is enabled.
    /// </summary>
    /// <remarks>
    /// Returns true when client certificate is configured for mutual authentication.
    /// Note: Having only <see cref="OtlpTlsOptions.CaCertificatePath"/> does not constitute mTLS.
    /// </remarks>
    public override bool IsMtlsEnabled =>
        !string.IsNullOrWhiteSpace(this.ClientCertificatePath);

    /// <summary>
    /// Gets a value indicating whether any TLS configuration is enabled.
    /// TLS is considered enabled if at least the client certificate path or CA path is provided.
    /// </summary>
    public bool IsEnabled => this.IsTlsEnabled || this.IsMtlsEnabled;
}

#endif
