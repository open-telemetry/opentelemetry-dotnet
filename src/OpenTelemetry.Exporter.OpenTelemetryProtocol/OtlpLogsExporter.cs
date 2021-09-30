// <copyright file="OtlpLogsExporter.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0 || NETSTANDARD2_1
using System;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OtlpCollector = Opentelemetry.Proto.Collector.Logs.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="LogRecord"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    internal class OtlpLogsExporter : BaseOtlpExporter<LogRecord>
    {
        private readonly OtlpCollector.LogsService.ILogsServiceClient logsClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpLogsExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpLogsExporter(OtlpExporterOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpLogsExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="logsServiceClient"><see cref="OtlpCollector.LogsService.LogsServiceClient"/>.</param>
        internal OtlpLogsExporter(OtlpExporterOptions options, OtlpCollector.LogsService.ILogsServiceClient logsServiceClient = null)
            : base(options)
        {
            if (logsServiceClient != null)
            {
                this.logsClient = logsServiceClient;
            }
            else
            {
                this.Channel = options.CreateChannel();
                this.logsClient = new OtlpCollector.LogsService.LogsServiceClient(this.Channel);
            }
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<LogRecord> logRecordBatch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            OtlpCollector.ExportLogsServiceRequest request = new OtlpCollector.ExportLogsServiceRequest();

            request.AddBatch(this.ProcessResource, logRecordBatch);
            var deadline = DateTime.UtcNow.AddMilliseconds(this.Options.TimeoutMilliseconds);

            try
            {
                this.logsClient.Export(request, headers: this.Headers, deadline: deadline);
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(ex);

                return ExportResult.Failure;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);

                return ExportResult.Failure;
            }

            return ExportResult.Success;
        }
    }
}
#endif
