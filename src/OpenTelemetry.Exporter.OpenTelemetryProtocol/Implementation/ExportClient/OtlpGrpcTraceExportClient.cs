// <copyright file="OtlpGrpcTraceExportClient.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Grpc.Core;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Class for sending OTLP trace export request over gRPC.</summary>
    internal sealed class OtlpGrpcTraceExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportTraceServiceRequest>
    {
        private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;

        public OtlpGrpcTraceExportClient(OtlpExporterOptions options, OtlpCollector.TraceService.TraceServiceClient traceServiceClient = null)
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
        public override bool SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

            try
            {
                this.traceClient.Export(request, headers: this.Headers, deadline: deadline, cancellationToken: cancellationToken);
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

                return false;
            }

            return true;
        }
    }
}
