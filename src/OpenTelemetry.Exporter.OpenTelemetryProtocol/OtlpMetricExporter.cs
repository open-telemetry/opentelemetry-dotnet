// <copyright file="OtlpMetricExporter.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Metrics;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Metric"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpMetricExporter : BaseExporter<Metric>
    {
        private readonly IExportClient<OtlpCollector.ExportMetricsServiceRequest> exportClient;

        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpMetricExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpMetricExporter(OtlpExporterOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpMetricExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the export.</param>
        /// <param name="exportClient">Client used for sending export request.</param>
        internal OtlpMetricExporter(OtlpExporterOptions options, IExportClient<OtlpCollector.ExportMetricsServiceRequest> exportClient = null)
        {
            if (exportClient != null)
            {
                this.exportClient = exportClient;
            }
            else
            {
                this.exportClient = options.GetMetricsExportClient();
            }
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

        /// <inheritdoc />
        public override ExportResult Export(in Batch<Metric> metrics)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            // [Performance] Need to allow for a message with partial data points but several metrics so we can still batch metrics in a single message
            //               and minimize the amount of requests to transmit all the data.
            var perMetricBatches = new List<OtlpMetrics.Metric>[metrics.Count];
            var meterNames = new Metric[metrics.Count]; // [abeaulieu] Would be nice to be able to do that without copying the metrics.

            var i = 0;
            foreach (var metric in metrics)
            {
                perMetricBatches[i] = metric.ToBatchedOtlpMetric().ToList();
                meterNames[i] = metric;
                ++i;
            }

            // [abeaulieu] Now loop through all the metrics until we've sent all the messages to send
            var done = false;
            var num_msg = 0;
            var wire_size = 0;
            while (!done)
            {
                var request = new OtlpCollector.ExportMetricsServiceRequest();

                // Try to make progress on every metric that still has data to send.
                for (i = 0; i < perMetricBatches.Length; ++i)
                {
                    done = true; // If all metrics have flushed their data, done will remain true and the loop exits.

                    var messages = perMetricBatches[i];
                    if (messages.Count > 0)
                    {
                        var m = messages[messages.Count - 1]; // Take the last one so there's no need to list copy to shift the list left.
                        messages.RemoveAt(messages.Count - 1); // [Perf]: Use a queue or just index manipulation to avoid memory shenanigans.
                        request.AddMetrics(this.ProcessResource, m, meterNames[i]);
                        done = false; // Not done until every metric has nothing to send.
                        num_msg++;
                    }
                }

                try
                {
                    wire_size += request.CalculateSize();

                    // [abeaulieu] This need to be refactored since we will need several requests now.
                    // request.AddMetrics(this.ProcessResource, metrics);
                    // [abeaulieu] TODO: Spread the request load?
                    if (!this.exportClient.SendExportRequest(request))
                    {
                        // Fail as soon as one export fails.
                        // [abeaulieu] Should we try the rest of the messages and only return the result at the end?
                        // Need to handle partial success here... what happens if only some payloads made it?
                        // Ideally the telemetry should be split into individual messages that get processed using the retry mechanism/
                        return ExportResult.Failure;
                    }
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
            }

            Console.WriteLine($"\nSent {num_msg} messages totaling {wire_size / 1024.0f / 1024.0f} MB of data");

            return ExportResult.Success;
        }

        /// <inheritdoc />
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
        }
    }
}
