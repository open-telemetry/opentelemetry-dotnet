// <copyright file="OtlpTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpTraceExporter : BaseOtlpExporter<Activity>
    {
        private readonly OtlpCollector.TraceService.ITraceServiceClient traceClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpTraceExporter(OtlpExporterOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="traceServiceClient"><see cref="OtlpCollector.TraceService.TraceServiceClient"/>.</param>
        internal OtlpTraceExporter(OtlpExporterOptions options, OtlpCollector.TraceService.ITraceServiceClient traceServiceClient = null)
            : base(options)
        {
            if (traceServiceClient != null)
            {
                this.traceClient = traceServiceClient;
            }
            else
            {
                this.Channel = options.CreateChannel();
                this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.Channel);
            }
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            OtlpCollector.ExportTraceServiceRequest request = new OtlpCollector.ExportTraceServiceRequest();

            request.AddBatch(this.ProcessResource, activityBatch);
            var deadline = DateTime.UtcNow.AddMilliseconds(this.Options.TimeoutMilliseconds);

            try
            {
                this.traceClient.Export(request, headers: this.Headers, deadline: deadline);
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
            finally
            {
                request.Return();
            }

            return ExportResult.Success;
        }
    }
}
