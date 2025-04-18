// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.X509Certificates;
#endif

namespace OpenTelemetry.Exporter;

/// <summary>
/// OpenTelemetry Protocol (OTLP) exporter options.
/// </summary>
/// <remarks>
/// Note: OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS,
/// OTEL_EXPORTER_OTLP_TIMEOUT, and OTEL_EXPORTER_OTLP_PROTOCOL environment
/// variables are parsed during object construction.
/// </remarks>
public class OtlpExporterOptions : IOtlpExporterOptions
{
    internal const string DefaultGrpcEndpoint = "http://localhost:4317";
    internal const string DefaultHttpEndpoint = "http://localhost:4318";
#if NET462_OR_GREATER || NETSTANDARD2_0
    internal const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.HttpProtobuf;
#else
    internal const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;
#endif

    internal const string CertificateFileEnvVarName = "OTEL_EXPORTER_OTLP_CERTIFICATE";
    internal const string ClientKeyFileEnvVarName = "OTEL_EXPORTER_OTLP_CLIENT_KEY";
    internal const string ClientCertificateFileEnvVarName = "OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE";

    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new("User-Agent", GetUserAgentString()),
    };

    internal readonly Func<HttpClient> DefaultHttpClientFactory;

    private OtlpExportProtocol? protocol;
    private Uri? endpoint;
    private int? timeoutMilliseconds;
    private Func<HttpClient>? httpClientFactory;
#if NET8_0_OR_GREATER
    private string certificateFilePath = string.Empty;
    private string clientKeyFilePath = string.Empty;
    private string clientCertificateFilePath = string.Empty;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
    /// </summary>
    public OtlpExporterOptions()
        : this(OtlpExporterOptionsConfigurationType.Default)
    {
    }

    internal OtlpExporterOptions(
        OtlpExporterOptionsConfigurationType configurationType)
        : this(
              configuration: new ConfigurationBuilder().AddEnvironmentVariables().Build(),
              configurationType,
              defaultBatchOptions: new())
    {
    }

    internal OtlpExporterOptions(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType,
        BatchExportActivityProcessorOptions defaultBatchOptions)
    {
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

        this.DefaultHttpClientFactory = () =>
        {
#if NET8_0_OR_GREATER
            var handler = new HttpClientHandler();
            HttpClient client = this.AddCertificatesToHttpClient(handler);
            client.Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds);
            return client;
#else
            // For earlier .NET versions
            return new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
            };
#endif
        };

        this.BatchExportProcessorOptions = defaultBatchOptions!;

        this.ApplyConfiguration(configuration, configurationType);

#if NET8_0_OR_GREATER
        // Load certificate-related environment variables
        this.CertificateFilePath = Environment.GetEnvironmentVariable(CertificateFileEnvVarName) ?? string.Empty;
        this.ClientKeyFilePath = Environment.GetEnvironmentVariable(ClientKeyFileEnvVarName) ?? string.Empty;
        this.ClientCertificateFilePath = Environment.GetEnvironmentVariable(ClientCertificateFileEnvVarName) ?? string.Empty;
#endif
    }

    /// <inheritdoc/>
    public Uri Endpoint
    {
        get
        {
            if (this.endpoint == null)
            {
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
                return this.Protocol == OtlpExportProtocol.Grpc
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
                    ? new Uri(DefaultGrpcEndpoint)
                    : new Uri(DefaultHttpEndpoint);
            }

            return this.endpoint;
        }

        set
        {
            Guard.ThrowIfNull(value);

            this.endpoint = value;
            this.AppendSignalPathToEndpoint = false;
        }
    }

    /// <inheritdoc/>
    public string? Headers { get; set; }

    /// <inheritdoc/>
    public int TimeoutMilliseconds
    {
        get => this.timeoutMilliseconds ?? 10000;
        set => this.timeoutMilliseconds = value;
    }

    /// <inheritdoc/>
    public OtlpExportProtocol Protocol
    {
        get => this.protocol ?? DefaultOtlpExportProtocol;
        set => this.protocol = value;
    }

    /// <summary>
    /// Gets or sets the export processor type to be used with the OpenTelemetry Protocol Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }

    /// <inheritdoc/>
    public Func<HttpClient> HttpClientFactory
    {
        get => this.httpClientFactory ?? this.DefaultHttpClientFactory;
        set
        {
            Guard.ThrowIfNull(value);

            this.httpClientFactory = value;
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets or sets the path to the trusted certificate file to use when verifying a server's TLS credentials.
    /// </summary>
    /// <remarks>
    /// This certificate will be used to validate the server's certificate. It must be in PEM format.
    /// This property is supported in .NET 8.0 or greater only.
    /// </remarks>
    public string CertificateFilePath
    {
        get => this.certificateFilePath;
        set => this.certificateFilePath = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the path to the private key file to use in mTLS communication.
    /// </summary>
    /// <remarks>
    /// This private key will be used for client authentication. It must be in PEM format.
    /// This property is supported in .NET 8.0 or greater only.
    /// </remarks>
    public string ClientKeyFilePath
    {
        get => this.clientKeyFilePath;
        set => this.clientKeyFilePath = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the path to the client certificate file to use in mTLS communication.
    /// </summary>
    /// <remarks>
    /// This certificate will be presented to the server for client authentication. It must be in PEM format.
    /// This property is supported in .NET 8.0 or greater only.
    /// </remarks>
    public string ClientCertificateFilePath
    {
        get => this.clientCertificateFilePath;
        set => this.clientCertificateFilePath = value ?? string.Empty;
    }
#endif

    /// <summary>
    /// Gets a value indicating whether or not the signal-specific path should
    /// be appended to <see cref="Endpoint"/>.
    /// </summary>
    /// <remarks>
    /// Note: Only applicable when <see cref="OtlpExportProtocol.HttpProtobuf"/>
    /// is used.
    /// </remarks>
    internal bool AppendSignalPathToEndpoint { get; private set; } = true;

    internal bool HasData
        => this.protocol.HasValue
        || this.endpoint != null
        || this.timeoutMilliseconds.HasValue
        || this.httpClientFactory != null;

    internal static OtlpExporterOptions CreateOtlpExporterOptions(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string name)
        => new(
            configuration,
            OtlpExporterOptionsConfigurationType.Default,
            serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name));

    internal void ApplyConfigurationUsingSpecificationEnvVars(
        IConfiguration configuration,
        string endpointEnvVarKey,
        bool appendSignalPathToEndpoint,
        string protocolEnvVarKey,
        string headersEnvVarKey,
        string timeoutEnvVarKey)
    {
        if (configuration.TryGetUriValue(endpointEnvVarKey, out var endpoint))
        {
            this.endpoint = endpoint;
            this.AppendSignalPathToEndpoint = appendSignalPathToEndpoint;
        }

        if (configuration.TryGetValue<OtlpExportProtocol>(
            protocolEnvVarKey,
            OtlpExportProtocolParser.TryParse,
            out var protocol))
        {
            this.Protocol = protocol;
        }

        if (configuration.TryGetStringValue(headersEnvVarKey, out var headers))
        {
            this.Headers = headers;
        }

        if (configuration.TryGetIntValue(timeoutEnvVarKey, out var timeout))
        {
            this.TimeoutMilliseconds = timeout;
        }
    }

    internal OtlpExporterOptions ApplyDefaults(OtlpExporterOptions defaultExporterOptions)
    {
        this.protocol ??= defaultExporterOptions.protocol;

        this.endpoint ??= defaultExporterOptions.endpoint;

        // Note: We leave AppendSignalPathToEndpoint set to true here because we
        // want to append the signal if the endpoint came from the default
        // endpoint.

        this.Headers ??= defaultExporterOptions.Headers;

        this.timeoutMilliseconds ??= defaultExporterOptions.timeoutMilliseconds;

        this.httpClientFactory ??= defaultExporterOptions.httpClientFactory;

        return this;
    }

#if NET8_0_OR_GREATER
    internal HttpClient AddCertificatesToHttpClient(HttpClientHandler handler)
    {
        try
        {
            // Configure server certificate validation if CertificateFilePath is provided
            if (!string.IsNullOrEmpty(this.CertificateFilePath))
            {
                try
                {
                    // Load the certificate with validation
                    var trustedCertificate = MTlsUtility.LoadCertificateWithValidation(this.CertificateFilePath);

                    // Set custom server certificate validation callback
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        if (cert != null && chain != null)
                        {
                            return MTlsUtility.ValidateCertificateChain(cert, trustedCertificate);
                        }

                        return false;
                    };

                    OpenTelemetryProtocolExporterEventSource.Log.MTlsConfigurationSuccess("HTTPS server validation");
                }
                catch (Exception ex)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                }
            }

            // Add client certificate if both files are provided
            if (!string.IsNullOrEmpty(this.ClientCertificateFilePath) && !string.IsNullOrEmpty(this.ClientKeyFilePath))
            {
                try
                {
                    var clientCertificate = MTlsUtility.LoadCertificateWithValidation(
                        this.ClientCertificateFilePath,
                        this.ClientKeyFilePath);

                    handler.ClientCertificates.Add(clientCertificate);
                    OpenTelemetryProtocolExporterEventSource.Log.MTlsConfigurationSuccess("HTTPS client authentication");
                }
                catch (Exception ex)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                }
            }

            // Create and return an HttpClient with the modified handler
            return new HttpClient(handler);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);

            // Fall back to a regular HttpClient without mTLS
            return new HttpClient();
        }
    }
#endif

    private static string GetUserAgentString()
    {
        return "OTel-OTLP-Exporter/1.0.0";
    }

    private void ApplyConfiguration(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType)
    {
        // Note: According to the specification, we should apply env vars in this exact order.
        // Ref: https://github.com/open-telemetry/opentelemetry-specification/blob/v1.23.0/specification/protocol/exporter.md#configuration-options

        // Apply OTLP level environment variables.
        ApplyConfigurationUsingSpecificationEnvVars(
            configuration,
            OtlpSpecConfigDefinitions.EndpointEnvVarName,
            appendSignalPathToEndpoint: true,
            OtlpSpecConfigDefinitions.ProtocolEnvVarName,
            OtlpSpecConfigDefinitions.HeadersEnvVarName,
            OtlpSpecConfigDefinitions.TimeoutEnvVarName);

        if (configurationType == OtlpExporterOptionsConfigurationType.Traces)
        {
            ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                OtlpSpecConfigDefinitions.TracesEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.TracesProtocolEnvVarName,
                OtlpSpecConfigDefinitions.TracesHeadersEnvVarName,
                OtlpSpecConfigDefinitions.TracesTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Metrics)
        {
            ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                OtlpSpecConfigDefinitions.MetricsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.MetricsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.MetricsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.MetricsTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Logs)
        {
            ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                OtlpSpecConfigDefinitions.LogsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.LogsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.LogsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.LogsTimeoutEnvVarName);
        }
    }
}
