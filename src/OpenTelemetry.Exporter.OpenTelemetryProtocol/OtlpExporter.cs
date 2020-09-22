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
using System.Collections.Generic;
using System.Diagnostics;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Trace;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpExporter : ActivityExporter
    {
        private readonly Channel channel;
        private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;
        private readonly Metadata headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpExporter(OtlpExporterOptions options)
        {
            this.headers = options?.Headers ?? throw new ArgumentNullException(nameof(options));
            this.channel = new Channel(options.Endpoint, options.Credentials, options.ChannelOptions);
            this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.channel);
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            var exporterRequest = new OtlpCollector.ExportTraceServiceRequest();

            var activities = new List<Activity>();
            foreach (var activity in activityBatch)
            {
                activities.Add(activity);
            }

            exporterRequest.ResourceSpans.AddRange(activities.ToOtlpResourceSpans());

            try
            {
                this.traceClient.Export(exporterRequest, headers: this.headers);
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(ex);

                return ExportResult.Failure;
            }

            return ExportResult.Success;
        }
    }
}
