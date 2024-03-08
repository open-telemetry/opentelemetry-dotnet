// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
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
public class OtlpExporterOptions
{
    internal const string DefaultGrpcEndpoint = "http://localhost:4317";
    internal const string DefaultHttpEndpoint = "http://localhost:4318";
    internal const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;

    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new KeyValuePair<string, string>("User-Agent", GetUserAgentString()),
    };

    internal readonly Func<HttpClient> DefaultHttpClientFactory;

    private const string UserAgentProduct = "OTel-OTLP-Exporter-Dotnet";

    private Uri endpoint;

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

        this.ApplyConfiguration(configuration, configurationType);

        this.HttpClientFactory = this.DefaultHttpClientFactory = () =>
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
            };
        };

        this.BatchExportProcessorOptions = defaultBatchOptions;
    }

    /// <summary>
    /// Gets or sets the target to which the exporter is going to send telemetry.
    /// Must be a valid Uri with scheme (http or https) and host, and
    /// may contain a port and path. The default value is
    /// * http://localhost:4317 for <see cref="OtlpExportProtocol.Grpc"/>
    /// * http://localhost:4318 for <see cref="OtlpExportProtocol.HttpProtobuf"/>.
    /// </summary>
    public Uri Endpoint
    {
        get
        {
            if (this.endpoint == null)
            {
                return this.Protocol == OtlpExportProtocol.Grpc
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

    /// <summary>
    /// Gets or sets optional headers for the connection. Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
    /// specification</a> for information on the expected format for Headers.
    /// </summary>
    public string Headers { get; set; }

    /// <summary>
    /// Gets or sets the max waiting time (in milliseconds) for the backend to process each batch. The default value is 10000.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the the OTLP transport protocol. Supported values: Grpc and HttpProtobuf.
    /// </summary>
    public OtlpExportProtocol Protocol { get; set; } = DefaultOtlpExportProtocol;

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
    /// Gets or sets the factory function called to create the <see
    /// cref="HttpClient"/> instance that will be used at runtime to
    /// transmit telemetry over HTTP. The returned instance will be reused
    /// for all export invocations.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is only invoked for the <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/> protocol.</item>
    /// <item>The default behavior when using the <see
    /// cref="OtlpTraceExporterHelperExtensions.AddOtlpExporter(TracerProviderBuilder,
    /// Action{OtlpExporterOptions})"/> extension is if an <a
    /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
    /// instance can be resolved through the application <see
    /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
    /// created through the factory with the name "OtlpTraceExporter"
    /// otherwise an <see cref="HttpClient"/> will be instantiated
    /// directly.</item>
    /// <item>The default behavior when using the <see
    /// cref="OtlpMetricExporterExtensions.AddOtlpExporter(MeterProviderBuilder,
    /// Action{OtlpExporterOptions})"/> extension is if an <a
    /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
    /// instance can be resolved through the application <see
    /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
    /// created through the factory with the name "OtlpMetricExporter"
    /// otherwise an <see cref="HttpClient"/> will be instantiated
    /// directly.</item>
    /// </list>
    /// </remarks>
    public Func<HttpClient> HttpClientFactory { get; set; }

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

    private static string GetUserAgentString()
    {
        try
        {
            var assemblyVersion = typeof(OtlpExporterOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var informationalVersion = assemblyVersion.InformationalVersion;
            return string.IsNullOrEmpty(informationalVersion) ? UserAgentProduct : $"{UserAgentProduct}/{informationalVersion}";
        }
        catch (Exception)
        {
            return UserAgentProduct;
        }
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
