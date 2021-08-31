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
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OtlpCollector = Opentelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="MetricItem"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpMetricsExporter : BaseOtlpExporter<MetricItem>
    {
        private readonly OtlpCollector.MetricsService.IMetricsServiceClient metricsClient;

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
            : base(options)
        {
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

        /// <inheritdoc />
        public override ExportResult Export(in Batch<MetricItem> batch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.AddBatch(this.ProcessResource, batch);
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
    }
}
