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

    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new("User-Agent", GetUserAgentString()),
    };

    internal readonly Func<HttpClient> DefaultHttpClientFactory;

    private OtlpExportProtocol? protocol;
    private Uri? endpoint;
    private int? timeoutMilliseconds;
    private Func<HttpClient>? httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
    /// </summary>
    public OtlpExporterOptions()
        : this(
            configuration: new ConfigurationBuilder().Build(),
            configurationType: OtlpExporterOptionsConfigurationType.Default,
            defaultBatchOptions: new())
    {
    }

    internal OtlpExporterOptions(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType,
        BatchExportActivityProcessorOptions defaultBatchOptions)
    {
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");
        var finalConfiguration = new ConfigurationBuilder()
            .AddEnvironmentVariables(OtlpSpecConfigDefinitions.EnvVarPrefix)
            .AddConfiguration(configuration)
            .Build();

        this.ApplyConfiguration(finalConfiguration, configurationType);

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
        if (configuration.TryGetUriValue(OpenTelemetryProtocolExporterEventSource.Log, endpointEnvVarKey, out var endpoint))
        {
            this.endpoint = endpoint;
            this.AppendSignalPathToEndpoint = appendSignalPathToEndpoint;
        }

        if (configuration.TryGetValue<OtlpExportProtocol>(
            OpenTelemetryProtocolExporterEventSource.Log,
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

        if (configuration.TryGetIntValue(OpenTelemetryProtocolExporterEventSource.Log, timeoutEnvVarKey, out var timeout))
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

    private static string GetUserAgentString()
    {
        var assembly = typeof(OtlpExporterOptions).Assembly;
        return $"OTel-OTLP-Exporter-Dotnet/{assembly.GetPackageVersion()}";
    }

    private void ApplyConfiguration(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType)
    {
        Debug.Assert(configuration != null, "configuration was null");

        // Note: When using the "AddOtlpExporter" extensions configurationType
        // never has a value other than "Default" because OtlpExporterOptions is
        // shared by all signals and there is no way to differentiate which
        // signal is being constructed.
        if (configurationType == OtlpExporterOptionsConfigurationType.Default)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName,
                appendSignalPathToEndpoint: true,
                OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName,
                OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName,
                OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Logs)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.LogsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.LogsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.LogsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.LogsTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Metrics)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.MetricsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.MetricsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.MetricsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.MetricsTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Traces)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.TracesEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.TracesProtocolEnvVarName,
                OtlpSpecConfigDefinitions.TracesHeadersEnvVarName,
                OtlpSpecConfigDefinitions.TracesTimeoutEnvVarName);
        }
        else
        {
            throw new NotSupportedException($"OtlpExporterOptionsConfigurationType '{configurationType}' is not supported.");
        }
    }
}
