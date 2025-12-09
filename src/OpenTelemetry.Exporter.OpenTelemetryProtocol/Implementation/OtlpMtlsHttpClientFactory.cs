// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Factory for creating HttpClient instances configured with mTLS settings.
/// </summary>
internal static class OtlpMtlsHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient configured with mTLS settings.
    /// </summary>
    /// <param name="mtlsOptions">The mTLS configuration options.</param>
    /// <param name="configureClient">Optional action to configure the client.</param>
    /// <returns>An HttpClient configured for mTLS.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mtlsOptions"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when mTLS is not enabled.</exception>
    public static HttpClient CreateMtlsHttpClient(
        OtlpMtlsOptions mtlsOptions,
        Action<HttpClient>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(mtlsOptions);

        if (!mtlsOptions.IsEnabled)
        {
            throw new InvalidOperationException("mTLS options must include a client or CA certificate path.");
        }

        HttpClientHandler? handler = null;
        X509Certificate2? caCertificate = null;
        X509Certificate2? clientCertificate = null;

        try
        {
            // Load certificates
            if (!string.IsNullOrEmpty(mtlsOptions.CaCertificatePath))
            {
                caCertificate = OtlpMtlsCertificateManager.LoadCaCertificate(
                    mtlsOptions.CaCertificatePath);

                if (mtlsOptions.EnableCertificateChainValidation)
                {
                    OtlpMtlsCertificateManager.ValidateCertificateChain(
                        caCertificate,
                        OtlpMtlsCertificateManager.CaCertificateType);
                }
            }

            if (!string.IsNullOrEmpty(mtlsOptions.ClientCertificatePath))
            {
                if (string.IsNullOrEmpty(mtlsOptions.ClientKeyPath))
                {
                    // Load certificate without separate key file (e.g., PKCS#12 format)
                    clientCertificate = OtlpMtlsCertificateManager.LoadClientCertificate(
                        mtlsOptions.ClientCertificatePath,
                        null);
                }
                else
                {
                    clientCertificate = OtlpMtlsCertificateManager.LoadClientCertificate(
                        mtlsOptions.ClientCertificatePath,
                        mtlsOptions.ClientKeyPath);
                }

                if (mtlsOptions.EnableCertificateChainValidation)
                {
                    OtlpMtlsCertificateManager.ValidateCertificateChain(
                        clientCertificate,
                        OtlpMtlsCertificateManager.ClientCertificateType);
                }

                OpenTelemetryProtocolExporterEventSource.Log.MtlsConfigurationEnabled(
                    clientCertificate.Subject);
            }

            // Create HttpClientHandler with mTLS configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - HttpClientHandler is disposed by HttpClient
            handler = new MtlsHttpClientHandler(clientCertificate, caCertificate);
#pragma warning restore CA2000
            handler.CheckCertificateRevocationList = true;

            // Handler now owns the certificates and will dispose them when disposed.
            caCertificate = null;
            clientCertificate = null;

            var client = new HttpClient(handler, disposeHandler: true);

            configureClient?.Invoke(client);

            return client;
        }
        catch (Exception ex)
        {
            // Dispose handler if something went wrong
            handler?.Dispose();

            OpenTelemetryProtocolExporterEventSource.Log.MtlsHttpClientCreationFailed(ex);
            throw;
        }
        finally
        {
            // Dispose certificates as they are no longer needed after being added to the handler
            caCertificate?.Dispose();
            clientCertificate?.Dispose();
        }
    }

    private sealed class MtlsHttpClientHandler : HttpClientHandler
    {
        private readonly X509Certificate2? caCertificate;
        private readonly X509Certificate2? clientCertificate;

        internal MtlsHttpClientHandler(
            X509Certificate2? clientCertificate,
            X509Certificate2? caCertificate)
        {
            this.clientCertificate = clientCertificate;
            this.caCertificate = caCertificate;
            this.CheckCertificateRevocationList = true;

            if (clientCertificate != null)
            {
                this.ClientCertificates.Add(clientCertificate);
                this.ClientCertificateOptions = ClientCertificateOption.Manual;
            }

            if (caCertificate != null)
            {
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

                    return OtlpMtlsCertificateManager.ValidateServerCertificate(
                        cert,
                        chain,
                        sslPolicyErrors,
                        caCertificate);
                };
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.caCertificate?.Dispose();
                this.clientCertificate?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

#endif
