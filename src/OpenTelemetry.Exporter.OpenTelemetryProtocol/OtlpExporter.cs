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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpExporter : BaseExporter<Activity>
    {
        private readonly OtlpExporterOptions options;
        private readonly Channel channel;
        private readonly OtlpCollector.TraceService.ITraceServiceClient traceClient;
        private readonly Metadata headers;
        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="traceServiceClient"><see cref="OtlpCollector.TraceService.TraceServiceClient"/>.</param>
        internal OtlpExporter(OtlpExporterOptions options, OtlpCollector.TraceService.ITraceServiceClient traceServiceClient = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.headers = options.Headers ?? throw new ArgumentException("Headers were not provided on options.", nameof(options));
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

            request.AddBatch(this, activityBatch);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal OtlpResource.Resource EnsureProcessResource(Activity activity)
        {
            if (this.processResource != null)
            {
                return this.processResource;
            }

            OtlpResource.Resource processResource = new OtlpResource.Resource();

            foreach (KeyValuePair<string, object> attribute in activity.GetResource().Attributes)
            {
                var oltpAttribute = attribute.ToOtlpAttribute();
                if (oltpAttribute != null)
                {
                    processResource.Attributes.Add(oltpAttribute);
                }
            }

            if (!processResource.Attributes.Any(kvp => kvp.Key == Resource.ServiceNameKey))
            {
                string serviceName = this.options.ServiceName;
                if (string.IsNullOrEmpty(serviceName))
                {
                    serviceName = OtlpExporterOptions.DefaultServiceName;
                }

                processResource.Attributes.Add(new OtlpCommon.KeyValue
                {
                    Key = Resource.ServiceNameKey,
                    Value = new OtlpCommon.AnyValue { StringValue = serviceName },
                });
            }

            return this.processResource = processResource;
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
