// <copyright file="OtlpMetricsExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Grpc.Core;
#if NETSTANDARD2_1
using Grpc.Net.Client;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OtlpCollector = Opentelemetry.Proto.Collector.Metrics.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Metric"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpMetricsExporter : MetricExporter
    {
        private readonly OtlpCollector.MetricsService.IMetricsServiceClient metricsClient;
        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpMetricsExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpMetricsExporter(OtlpExporterOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpMetricsExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        /// <param name="metricsServiceClient"><see cref="OtlpCollector.MetricsService.IMetricsServiceClient"/>.</param>
        internal OtlpMetricsExporter(OtlpExporterOptions options, OtlpCollector.MetricsService.IMetricsServiceClient metricsServiceClient = null)
        {
            this.Options = options ?? throw new ArgumentNullException(nameof(options));
            this.Headers = options.GetMetadataFromHeaders();
            if (this.Options.TimeoutMilliseconds <= 0)
            {
                throw new ArgumentException("Timeout value provided is not a positive number.", nameof(this.Options.TimeoutMilliseconds));
            }

            if (metricsServiceClient != null)
            {
                this.metricsClient = metricsServiceClient;
            }
            else
            {
                this.Channel = options.CreateChannel();
                this.metricsClient = new OtlpCollector.MetricsService.MetricsServiceClient(this.Channel);
            }
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

#if NETSTANDARD2_1
        internal GrpcChannel Channel { get; set; }
#else
        internal Channel Channel { get; set; }
#endif

        internal OtlpExporterOptions Options { get; }

        internal Metadata Headers { get; }

        /// <inheritdoc />
        public override AggregationTemporality GetAggregationTemporality()
        {
            return this.Options.AggregationTemporality;
        }

        /// <inheritdoc />
        public override ExportResult Export(IEnumerable<Metric> metrics)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.AddBatch(this.ProcessResource, metrics);
            var deadline = DateTime.UtcNow.AddMilliseconds(this.Options.TimeoutMilliseconds);

            try
            {
                this.metricsClient.Export(request, headers: this.Headers, deadline: deadline);
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

        /// <inheritdoc />
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            if (this.Channel == null)
            {
                return true;
            }

            if (timeoutMilliseconds == -1)
            {
                this.Channel.ShutdownAsync().Wait();
                return true;
            }
            else
            {
                return Task.WaitAny(new Task[] { this.Channel.ShutdownAsync(), Task.Delay(timeoutMilliseconds) }) == 0;
            }
        }
    }
}
