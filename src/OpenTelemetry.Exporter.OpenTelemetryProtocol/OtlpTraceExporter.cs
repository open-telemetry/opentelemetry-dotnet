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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
#if NETSTANDARD2_1
using Grpc.Net.Client;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Resources;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpTraceExporter : BaseExporter<Activity>
    {
        private const string DefaultServiceName = "OpenTelemetry Exporter";

        private readonly OtlpExporterOptions options;
#if NETSTANDARD2_1
        private readonly GrpcChannel channel;
#else
        private readonly Channel channel;
#endif
        private readonly OtlpCollector.TraceService.ITraceServiceClient traceClient;
        private readonly Metadata headers;

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
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.headers = options.Headers ?? throw new ArgumentException("Headers were not provided on options.", nameof(options));
            if (traceServiceClient != null)
            {
                this.traceClient = traceServiceClient;
            }
            else
            {
#if NETSTANDARD2_1
                this.channel = options.GrpcChannelOptions == default
                    ? GrpcChannel.ForAddress(options.Endpoint)
                    : GrpcChannel.ForAddress(options.Endpoint, options.GrpcChannelOptions);
#else
                this.channel = new Channel(options.Endpoint, options.Credentials, options.ChannelOptions);
#endif
                this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.channel);
            }
        }

        internal OtlpResource.Resource ProcessResource { get; private set; }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            if (this.ProcessResource == null)
            {
                this.SetResource(this.ParentProvider.GetResource());
            }

            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            OtlpCollector.ExportTraceServiceRequest request = new OtlpCollector.ExportTraceServiceRequest();

            request.AddBatch(this.ProcessResource, activityBatch);

            try
            {
                this.traceClient.Export(request, headers: this.headers);
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

        internal void SetResource(Resource resource)
        {
            OtlpResource.Resource processResource = new OtlpResource.Resource();

            foreach (KeyValuePair<string, object> attribute in resource.Attributes)
            {
                var oltpAttribute = attribute.ToOtlpAttribute();
                if (oltpAttribute != null)
                {
                    processResource.Attributes.Add(oltpAttribute);
                }
            }

            if (!processResource.Attributes.Any(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName))
            {
                processResource.Attributes.Add(new OtlpCommon.KeyValue
                {
                    Key = ResourceSemanticConventions.AttributeServiceName,
                    Value = new OtlpCommon.AnyValue { StringValue = DefaultServiceName },
                });
            }

            this.ProcessResource = processResource;
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
