// <copyright file="OtlpLogExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="LogRecord"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
internal sealed class OtlpLogExporter : BaseExporter<LogRecord>
{
    private readonly IExportClient<OtlpCollector.ExportLogsServiceRequest> exportClient;
    private readonly OtlpLogRecordProcessor otlpLogRecordProcessor;

    private OtlpResource.Resource processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public OtlpLogExporter(OtlpExporterOptions options)
        : this(options, new(), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions">Configuration options for the exporter.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="exportClient">Client used for sending export request.</param>
    internal OtlpLogExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        IExportClient<OtlpCollector.ExportLogsServiceRequest> exportClient = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");

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
            this.exportClient = exporterOptions.GetLogExportClient();
        }

        this.otlpLogRecordProcessor = new OtlpLogRecordProcessor(sdkLimitOptions, new());
    }

    internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> logRecordBatch)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        try
        {
            var request = this.otlpLogRecordProcessor.BuildExportRequest(this.ProcessResource, logRecordBatch);

            if (!this.exportClient.SendExportRequest(request))
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
