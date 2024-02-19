// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="Metric"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
public class OtlpMetricExporter : BaseExporter<Metric>
{
    private readonly IExportClient<OtlpCollector.ExportMetricsServiceRequest> exportClient;

    private OtlpResource.Resource processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpMetricExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public OtlpMetricExporter(OtlpExporterOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpMetricExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    /// <param name="exportClient">Client used for sending export request.</param>
    internal OtlpMetricExporter(OtlpExporterOptions options, IExportClient<OtlpCollector.ExportMetricsServiceRequest> exportClient = null)
    {
        // Each of the Otlp exporters: Traces, Metrics, and Logs set the same value for `OtlpKeyValueTransformer.LogUnsupportedAttributeType`
        // and `ConfigurationExtensions.LogInvalidEnvironmentVariable` so it should be fine even if these exporters are used together.
        OtlpKeyValueTransformer.LogUnsupportedAttributeType = (string tagValueType, string tagKey) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(tagValueType, tagKey);
        };

        ConfigurationExtensions.LogInvalidEnvironmentVariable = (string key, string value) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.InvalidEnvironmentVariable(key, value);
        };

        if (exportClient != null)
        {
            this.exportClient = exportClient;
        }
        else
        {
            this.exportClient = options.GetMetricsExportClient();
        }
    }

    internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        try
        {
            request.AddMetrics(this.ProcessResource, metrics);

            if (!this.exportClient.SendExportRequest(request).Success)
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }
        finally
        {
            request.Return();
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
