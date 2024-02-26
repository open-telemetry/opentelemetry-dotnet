// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

public class OtlpExporterOptionsBase
{
    internal const string EndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
    internal const string ProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";
    internal const string HeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
    internal const string TimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";

    internal readonly Func<HttpClient> DefaultHttpClientFactory;
    internal bool ProgrammaticallyModifiedEndpoint;

    private const string DefaultGrpcEndpoint = "http://localhost:4317";
    private const string DefaultHttpEndpoint = "http://localhost:4318";
    private const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;

    private OtlpExportProtocol? protocol;
    private Uri? endpoint;
    private int? timeoutMilliseconds;
    private Func<HttpClient>? httpClientFactory;

    internal OtlpExporterOptionsBase(
        IConfiguration configuration,
        OtlpExporterSignals signal)
    {
        Debug.Assert(configuration != null, "configuration was null");

        if (signal == OtlpExporterSignals.None)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                EndpointEnvVarName,
                ProtocolEnvVarName,
                HeadersEnvVarName,
                TimeoutEnvVarName);
        }

        if (signal.HasFlag(OtlpExporterSignals.Logs))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
                "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL",
                "OTEL_EXPORTER_OTLP_LOGS_HEADERS",
                "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT");
        }

        if (signal.HasFlag(OtlpExporterSignals.Metrics))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
                "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL",
                "OTEL_EXPORTER_OTLP_METRICS_HEADERS",
                "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT");
        }

        if (signal.HasFlag(OtlpExporterSignals.Traces))
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration,
                "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
                "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL",
                "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
                "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT");
        }

        this.DefaultHttpClientFactory = () =>
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
            };
        };
    }

    /// <summary>
    /// Gets or sets the the OTLP transport protocol. Supported values: Grpc and HttpProtobuf.
    /// </summary>
    public OtlpExportProtocol Protocol
    {
        get => this.protocol ?? DefaultOtlpExportProtocol;
        set => this.protocol = value;
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
                this.endpoint = this.Protocol == OtlpExportProtocol.Grpc
                    ? new Uri(DefaultGrpcEndpoint)
                    : new Uri(DefaultHttpEndpoint);
            }

            return this.endpoint;
        }

        set
        {
            this.endpoint = value;
            this.ProgrammaticallyModifiedEndpoint = true;
        }
    }

    /// <summary>
    /// Gets or sets optional headers for the connection. Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
    /// specification</a> for information on the expected format for Headers.
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// Gets or sets the max waiting time (in milliseconds) for the backend to process each batch. The default value is 10000.
    /// </summary>
    public int TimeoutMilliseconds
    {
        get => this.timeoutMilliseconds ?? 10000;
        set => this.timeoutMilliseconds = value;
    }

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

    internal OtlpExporterOptionsBase ApplyDefaults(OtlpExporterOptionsBase defaultInstance)
    {
        this.protocol ??= defaultInstance.protocol;

        this.endpoint ??= defaultInstance.endpoint;

        // Note: We don't set ProgrammaticallyModifiedEndpoint because we
        // want to append the signal if the endpoint came from the default
        // endpoint.

        this.Headers ??= defaultInstance.Headers;

        this.timeoutMilliseconds ??= defaultInstance.timeoutMilliseconds;

        this.httpClientFactory ??= defaultInstance.httpClientFactory;

        return this;
    }

    private void ApplyConfigurationUsingSpecificationEnvVars(
        IConfiguration configuration,
        string endpointEnvVarKey,
        string protocolEnvVarKey,
        string headersEnvVarKey,
        string timeoutEnvVarKey)
    {
        if (configuration.TryGetUriValue(endpointEnvVarKey, out var endpoint))
        {
            this.endpoint = endpoint;
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
