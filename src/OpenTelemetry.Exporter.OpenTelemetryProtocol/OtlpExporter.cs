// <copyright file="OtlpExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpExporter : BaseExporter<Activity>
    {
        private readonly Channel channel;
        private readonly OtlpCollector.TraceService.ITraceServiceClient traceClient;
        private readonly Metadata headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="traceServiceClient"><see cref="OtlpCollector.TraceService.TraceServiceClient"/>.</param>
        internal OtlpExporter(OtlpExporterOptions options, OtlpCollector.TraceService.ITraceServiceClient traceServiceClient = null)
        {
            this.headers = options?.Headers ?? throw new ArgumentNullException(nameof(options));
            if (traceServiceClient != null)
            {
                this.traceClient = traceServiceClient;
            }
            else
            {
                this.channel = new Channel(options.Endpoint, options.Credentials, options.ChannelOptions);
                this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.channel);
            }
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            OtlpCollector.ExportTraceServiceRequest request = new OtlpCollector.ExportTraceServiceRequest();

            request.AddBatch(activityBatch);

            try
            {
                this.traceClient.Export(request, headers: this.headers);
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(ex);

                return ExportResult.Failure;
            }
            finally
            {
                request.Return();
            }

            return ExportResult.Success;
        }

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            if (this.channel == null)
            {
                return true;
            }

            return Task.WaitAny(new Task[] { this.channel.ShutdownAsync(), Task.Delay(timeoutMilliseconds) }) == 0;
        }
    }
}
