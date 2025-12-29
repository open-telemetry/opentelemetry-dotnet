// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Factory for creating HttpClient instances configured with TLS settings.
/// </summary>
internal static class OtlpSecureHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient configured with TLS settings based on the provided options.
    /// </summary>
    /// <param name="tlsOptions">The TLS configuration options.</param>
    /// <param name="configureClient">Optional action to configure the client.</param>
    /// <returns>An HttpClient configured for secure communication.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tlsOptions"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when TLS is not enabled.</exception>
    public static HttpClient CreateSecureHttpClient(
        OtlpTlsOptions tlsOptions,
        Action<HttpClient>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(tlsOptions);

        if (!tlsOptions.IsTlsEnabled && !tlsOptions.IsMtlsEnabled)
        {
            throw new InvalidOperationException(
                "TLS options must include at least a CA path or client certificate path.");
        }

        X509Certificate2? caCertificate = null;
        X509Certificate2? clientCertificate = null;
        TlsHttpClientHandler? handler = null;

        try
        {
            if (!string.IsNullOrEmpty(tlsOptions.CaCertificatePath))
            {
                caCertificate = OtlpCertificateManager.LoadCaCertificate(
                    tlsOptions.CaCertificatePath);

                if (tlsOptions.EnableCertificateChainValidation)
                {
                    OtlpCertificateManager.ValidateCertificateChain(
                        caCertificate,
                        OtlpCertificateManager.CaCertificateType);
                }
            }

            if (tlsOptions is OtlpMtlsOptions mtlsOptions && mtlsOptions.IsMtlsEnabled)
            {
                clientCertificate = string.IsNullOrEmpty(mtlsOptions.ClientKeyPath)
                    ? OtlpCertificateManager.LoadClientCertificate(
                        mtlsOptions.ClientCertificatePath!,
                        null)
                    : OtlpCertificateManager.LoadClientCertificate(
                        mtlsOptions.ClientCertificatePath!,
                        mtlsOptions.ClientKeyPath);

                if (tlsOptions.EnableCertificateChainValidation)
                {
                    OtlpCertificateManager.ValidateCertificateChain(
                        clientCertificate,
                        OtlpCertificateManager.ClientCertificateType);
                }

                OpenTelemetryProtocolExporterEventSource.Log.MtlsConfigurationEnabled(
                    clientCertificate.Subject);
            }
            else if (caCertificate != null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.CaCertificateConfigured(
                    caCertificate.Subject);
            }

            // Create HttpClientHandler and apply TLS configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - HttpClientHandler is disposed by HttpClient
            handler = new TlsHttpClientHandler(caCertificate, clientCertificate);
#pragma warning restore CA2000

            // Handler now owns certificates.
            caCertificate = null;
            clientCertificate = null;

#pragma warning disable CA5399 // CheckCertificateRevocationList is set in ConfigureTls.
            var client = new HttpClient(handler, disposeHandler: true);
#pragma warning restore CA5399

            configureClient?.Invoke(client);

            return client;
        }
        catch (Exception ex)
        {
            handler?.Dispose();
            OpenTelemetryProtocolExporterEventSource.Log.SecureHttpClientCreationFailed(ex);
            throw;
        }
        finally
        {
            // Cleanup if ownership was not transferred to the handler.
            caCertificate?.Dispose();
            clientCertificate?.Dispose();
        }
    }

    /// <summary>
    /// Creates an HttpClient configured with mTLS settings.
    /// </summary>
    /// <param name="mtlsOptions">The mTLS configuration options.</param>
    /// <param name="configureClient">Optional action to configure the client.</param>
    /// <returns>An HttpClient configured for mTLS.</returns>
    /// <remarks>
    /// This method exists for backward compatibility. New code should use
    /// <see cref="CreateSecureHttpClient(OtlpTlsOptions, Action{HttpClient}?)"/>.
    /// </remarks>
    public static HttpClient CreateMtlsHttpClient(
        OtlpMtlsOptions mtlsOptions,
        Action<HttpClient>? configureClient = null)
    {
        return CreateSecureHttpClient(mtlsOptions, configureClient);
    }

    /// <summary>
    /// HttpClientHandler that applies TLS configuration based on loaded certificates.
    /// </summary>
    private sealed class TlsHttpClientHandler : HttpClientHandler
    {
        private readonly X509Certificate2? caCertificate;
        private readonly X509Certificate2? clientCertificate;
        private bool disposed;

        internal TlsHttpClientHandler(
            X509Certificate2? caCertificate,
            X509Certificate2? clientCertificate)
        {
            this.caCertificate = caCertificate;
            this.clientCertificate = clientCertificate;

            this.ConfigureTls();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.clientCertificate?.Dispose();
                this.caCertificate?.Dispose();
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void ConfigureTls()
        {
            this.CheckCertificateRevocationList = true;

            this.ConfigureClientCertificate();
            this.ConfigureCaCertificateValidation();
        }

        private void ConfigureClientCertificate()
        {
            if (this.clientCertificate == null)
            {
                return;
            }

            this.ClientCertificates.Add(this.clientCertificate);
            this.ClientCertificateOptions = ClientCertificateOption.Manual;
        }

        private void ConfigureCaCertificateValidation()
        {
            if (this.caCertificate == null)
            {
                return;
            }

            var caCert = this.caCertificate;
            this.ServerCertificateCustomValidationCallback = (
                httpRequestMessage,
                cert,
                chain,
                sslPolicyErrors) =>
            {
                if (cert == null || chain == null)
                {
                    return false;
                }

                return OtlpCertificateManager.ValidateServerCertificate(
                    cert,
                    chain,
                    sslPolicyErrors,
                    caCert);
            };
        }
    }
}

#endif
