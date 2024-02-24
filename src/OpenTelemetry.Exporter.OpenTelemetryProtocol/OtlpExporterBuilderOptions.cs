// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public sealed class OtlpExporterBuilderOptions
{
    internal OtlpExporterBuilderOptions(
        IConfiguration configuration,
        ActivityExportProcessorOptions defaultExportProcessorOptions)
    {
        this.DefaultOptions = new OtlpExporterOptions(configuration, defaultExportProcessorOptions);

        var emptyConfiguration = new ConfigurationBuilder().Build();

        this.LoggingOptions = new OtlpExporterOptions(emptyConfiguration, defaultExportProcessorOptions: null);

        this.MetricsOptions = new OtlpExporterOptions(emptyConfiguration, defaultExportProcessorOptions: null);

        this.TracingOptions = new OtlpExporterOptions(emptyConfiguration, defaultExportProcessorOptions);

        if (configuration.TryGetUriValue("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT", out var endpoint))
        {
            this.LoggingOptions.Endpoint = endpoint;
        }

        if (configuration.TryGetValue<OtlpExportProtocol>(
            "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL",
            OtlpExportProtocolParser.TryParse,
            out var protocol))
        {
            this.LoggingOptions.Protocol = protocol;
        }

        if (configuration.TryGetUriValue("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", out endpoint))
        {
            this.MetricsOptions.Endpoint = endpoint;
        }

        if (configuration.TryGetValue(
            "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL",
            OtlpExportProtocolParser.TryParse,
            out protocol))
        {
            this.MetricsOptions.Protocol = protocol;
        }

        if (configuration.TryGetUriValue("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", out endpoint))
        {
            this.TracingOptions.Endpoint = endpoint;
        }

        if (configuration.TryGetValue(
            "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL",
            OtlpExportProtocolParser.TryParse,
            out protocol))
        {
            this.TracingOptions.Protocol = protocol;
        }
    }

    public bool AddToLoggerProvider { get; set; } = true;

    public bool AddToMeterProvider { get; set; } = true;

    public bool AddToTracerProvider { get; set; } = true;

    public OtlpExporterOptions DefaultOptions { get; }

    public OtlpExporterOptions LoggingOptions { get; }

    public OtlpExporterOptions MetricsOptions { get; }

    public OtlpExporterOptions TracingOptions { get; }
}
