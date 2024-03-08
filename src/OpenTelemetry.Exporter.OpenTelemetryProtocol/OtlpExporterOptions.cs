// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

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
    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new KeyValuePair<string, string>("User-Agent", GetUserAgentString()),
    };

    internal readonly Func<HttpClient> DefaultHttpClientFactory;

    private const string DefaultGrpcEndpoint = "http://localhost:4317";
    private const string DefaultHttpEndpoint = "http://localhost:4318";
    private const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;
    private const string UserAgentProduct = "OTel-OTLP-Exporter-Dotnet";

    private OtlpExportProtocol? protocol;
    private Uri? endpoint;
    private int? timeoutMilliseconds;
    private Func<HttpClient>? httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
    /// </summary>
    public OtlpExporterOptions()
        : this(
              configuration: new ConfigurationBuilder().AddEnvironmentVariables().Build(),
              signal: OtlpExporterSignals.None,
              defaultBatchOptions: new())
    {
    }

    internal OtlpExporterOptions(
        IConfiguration configuration,
        OtlpExporterSignals signal,
        BatchExportActivityProcessorOptions defaultBatchOptions)
    {
        Debug.Assert(configuration != null, "configuration was null");
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

        if (signal == OtlpExporterSignals.None)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpExporterSpecEnvVarKeyDefinitions.DefaultEndpointEnvVarName,
                appendSignalPathToEndpoint: true,
                OtlpExporterSpecEnvVarKeyDefinitions.DefaultProtocolEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.DefaultHeadersEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.DefaultTimeoutEnvVarName);
        }

        if (signal.HasFlag(OtlpExporterSignals.Logs))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpExporterSpecEnvVarKeyDefinitions.LogsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpExporterSpecEnvVarKeyDefinitions.LogsProtocolEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.LogsHeadersEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.LogsTimeoutEnvVarName);
        }

        if (signal.HasFlag(OtlpExporterSignals.Metrics))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpExporterSpecEnvVarKeyDefinitions.MetricsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpExporterSpecEnvVarKeyDefinitions.MetricsProtocolEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.MetricsHeadersEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.MetricsTimeoutEnvVarName);
        }

        if (signal.HasFlag(OtlpExporterSignals.Traces))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpExporterSpecEnvVarKeyDefinitions.TracesEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpExporterSpecEnvVarKeyDefinitions.TracesProtocolEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.TracesHeadersEnvVarName,
                OtlpExporterSpecEnvVarKeyDefinitions.TracesTimeoutEnvVarName);
        }

        this.DefaultHttpClientFactory = () =>
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
            };
        };

        this.BatchExportProcessorOptions = defaultBatchOptions!;
    }

    /// <inheritdoc/>
    public OtlpExportProtocol Protocol
    {
        get => this.protocol ?? DefaultOtlpExportProtocol;
        set => this.protocol = value;
    }

    /// <inheritdoc/>
    public Uri Endpoint
    {
        get
        {
            if (this.endpoint == null)
            {
                this.endpoint = this.Protocol == OtlpExportProtocol.Grpc
                    ? new Uri(DefaultGrpcEndpoint)
                    : new Uri(DefaultHttpEndpoint);
            }

            return this.endpoint;
        }

        set
        {
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
    public Func<HttpClient> HttpClientFactory
    {
        get => this.httpClientFactory ??= this.DefaultHttpClientFactory;
        set
        {
            this.httpClientFactory = value ?? NullHttpClientFactory;

            static HttpClient NullHttpClientFactory()
            {
                return null!;
            }
        }
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

    /// <summary>
    /// Gets a value indicating whether or not the signal-specific path should
    /// be appended to <see cref="Endpoint"/>.
    /// </summary>
    /// <remarks>
    /// Note: Only applicable when <see cref="OtlpExportProtocol.HttpProtobuf"/>
    /// is used.
    /// </remarks>
    internal bool AppendSignalPathToEndpoint { get; private set; } = true;

    internal static void RegisterOtlpExporterOptionsFactory(IServiceCollection services)
    {
        services.RegisterOptionsFactory(CreateOtlpExporterOptions);
    }

    internal static OtlpExporterOptions CreateOtlpExporterOptions(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string name)
        => new(
            configuration,
            OtlpExporterSignals.None,
            serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name));

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

    private static string GetUserAgentString()
    {
        try
        {
            var assemblyVersion = typeof(OtlpExporterOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var informationalVersion = assemblyVersion?.InformationalVersion;
            return string.IsNullOrEmpty(informationalVersion) ? UserAgentProduct : $"{UserAgentProduct}/{informationalVersion}";
        }
        catch (Exception)
        {
            return UserAgentProduct;
        }
    }

    private void ApplyConfigurationUsingSpecificationEnvVars(
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
            if (!appendSignalPathToEndpoint)
            {
                this.AppendSignalPathToEndpoint = false;
            }
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
}
