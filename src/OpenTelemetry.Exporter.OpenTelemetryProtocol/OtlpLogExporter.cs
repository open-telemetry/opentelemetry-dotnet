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

using System;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Logs;
using OtlpCollector = Opentelemetry.Proto.Collector.Logs.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="LogRecord"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    internal class OtlpLogExporter : BaseExporter<LogRecord>
    {
        private readonly IExportClient<OtlpCollector.ExportLogsServiceRequest> exportClient;

        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpLogExporter(OtlpExporterOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="exportClient">Client used for sending export request.</param>
        internal OtlpLogExporter(OtlpExporterOptions options, IExportClient<OtlpCollector.ExportLogsServiceRequest> exportClient = null)
        {
            if (exportClient != null)
            {
                this.exportClient = exportClient;
            }
            else
            {
                this.exportClient = options.GetLogExportClient();
            }
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<LogRecord> logRecordBatch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            var request = new OtlpCollector.ExportLogsServiceRequest();

            try
            {
                request.AddBatch(this.ProcessResource, logRecordBatch);

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
}
