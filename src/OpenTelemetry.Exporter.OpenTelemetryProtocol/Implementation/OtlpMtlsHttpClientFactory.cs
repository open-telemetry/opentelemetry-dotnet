// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

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
    /// <param name="baseFactory">The base HttpClient factory to use.</param>
    /// <returns>An HttpClient configured for mTLS.</returns>
    public static HttpClient CreateMtlsHttpClient(
        OtlpMtlsOptions mtlsOptions,
        Func<HttpClient> baseFactory)
    {
        ArgumentNullException.ThrowIfNull(mtlsOptions);
        ArgumentNullException.ThrowIfNull(baseFactory);

        if (!mtlsOptions.IsEnabled)
        {
            return baseFactory();
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
                    mtlsOptions.CaCertificatePath,
                    mtlsOptions.EnableFilePermissionChecks);

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
                    // Check if certificate file exists to provide appropriate error message
                    if (!File.Exists(mtlsOptions.ClientCertificatePath))
                    {
                        throw new FileNotFoundException($"Certificate file not found at path: {mtlsOptions.ClientCertificatePath}", mtlsOptions.ClientCertificatePath);
                    }
                }
                else
                {
                    clientCertificate = OtlpMtlsCertificateManager.LoadClientCertificate(
                        mtlsOptions.ClientCertificatePath,
                        mtlsOptions.ClientKeyPath,
                        mtlsOptions.ClientKeyPassword,
                        mtlsOptions.EnableFilePermissionChecks);

                    if (mtlsOptions.EnableCertificateChainValidation)
                    {
                        OtlpMtlsCertificateManager.ValidateCertificateChain(
                            clientCertificate,
                            OtlpMtlsCertificateManager.ClientCertificateType);
                    }

                    OpenTelemetryProtocolExporterEventSource.Log.MtlsConfigurationEnabled(
                        clientCertificate.Subject);
                }
            }

            // Create HttpClientHandler with mTLS configuration
#pragma warning disable CA2000 // Dispose objects before losing scope - HttpClientHandler is disposed by HttpClient
            handler = new HttpClientHandler { CheckCertificateRevocationList = true };
#pragma warning restore CA2000

            // Add client certificate if available
            if (clientCertificate != null)
            {
                handler.ClientCertificates.Add(clientCertificate);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            }

            // Set up server certificate validation
            if (caCertificate != null)
            {
                handler.ServerCertificateCustomValidationCallback = (
                    httpRequestMessage,
                    cert,
                    chain,
                    sslPolicyErrors) =>
                {
                    if (cert == null || chain == null)
                    {
                        return false;
                    }

                    var serverCertValidationCallback =
                        OtlpMtlsCertificateManager.CreateServerCertificateValidationCallback(
                            caCertificate);
                    return serverCertValidationCallback(cert, chain, sslPolicyErrors);
                };
            }
            else if (mtlsOptions.ServerCertificateValidationCallback != null)
            {
                handler.ServerCertificateCustomValidationCallback = (
                    httpRequestMessage,
                    cert,
                    chain,
                    sslPolicyErrors) =>
                {
                    if (cert == null || chain == null)
                    {
                        return false;
                    }

                    return mtlsOptions.ServerCertificateValidationCallback(
                        cert,
                        chain,
                        sslPolicyErrors);
                };
            }

            var client = new HttpClient(handler, disposeHandler: true);

            ConfigureHttpClientDefaults(client, baseFactory);

            // Dispose certificates as they are no longer needed after being added to the handler
            caCertificate?.Dispose();
            clientCertificate?.Dispose();

            return client;
        }
        catch (Exception ex)
        {
            // Dispose resources if something went wrong
            handler?.Dispose();
            caCertificate?.Dispose();
            clientCertificate?.Dispose();

            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            throw;
        }
    }

    /// <summary>
    /// Creates an HttpClient factory that supports mTLS configuration.
    /// </summary>
    /// <param name="mtlsOptions">The mTLS configuration options.</param>
    /// <param name="baseFactory">The base HttpClient factory to use.</param>
    /// <returns>A factory function that creates mTLS-configured HttpClient instances.</returns>
    public static Func<HttpClient> CreateMtlsHttpClientFactory(
        OtlpMtlsOptions? mtlsOptions,
        Func<HttpClient> baseFactory)
    {
        if (mtlsOptions == null || !mtlsOptions.IsEnabled)
        {
            return baseFactory;
        }

        return () => CreateMtlsHttpClient(mtlsOptions, baseFactory);
    }

    /// <summary>
    /// Configures the HttpClient with default settings from the base factory.
    /// </summary>
    /// <param name="client">The HttpClient to configure.</param>
    /// <param name="baseFactory">The base HttpClient factory to get settings from.</param>
    private static void ConfigureHttpClientDefaults(HttpClient client, Func<HttpClient> baseFactory)
    {
        // Get base HttpClient to copy settings
        using var baseClient = baseFactory();

        // Copy settings from base client
        client.Timeout = baseClient.Timeout;
        client.BaseAddress = baseClient.BaseAddress;

        // Copy default headers
        foreach (var header in baseClient.DefaultRequestHeaders)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
    }
}

#endif
