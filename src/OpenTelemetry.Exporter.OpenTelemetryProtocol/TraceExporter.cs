// <copyright file="TraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Trace.Export;

using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// The trace exporter using the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class TraceExporter : SpanExporter
    {
        private readonly Channel channel;
        private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public TraceExporter(ExporterOptions options)
        {
            this.channel = new Channel(options.Endpoint, options.Credentials);
            this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.channel);
        }

        /// <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(
            IEnumerable<SpanData> spanDataList,
            CancellationToken cancellationToken)
        {
            var spanExportRequest = new OtlpCollector.ExportTraceServiceRequest();
            spanExportRequest.ResourceSpans.AddRange(spanDataList.ToOtlpResourceSpans());

            try
            {
                await this.traceClient.ExportAsync(spanExportRequest);
            }
            catch (RpcException ex)
            {
                ExporterEventSource.Log.FailedToReachCollector(ex);

                return ExportResult.FailedRetryable;
            }

            return ExportResult.Success;
        }

        /// <inheritdoc/>
        public override async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            await this.channel.ShutdownAsync().ConfigureAwait(false);
        }
    }
}
