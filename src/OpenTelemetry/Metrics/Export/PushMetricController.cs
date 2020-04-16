// <copyright file="PushMetricController.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics.Configuration;
using static OpenTelemetry.Metrics.Configuration.MeterFactory;

namespace OpenTelemetry.Metrics.Export
{
    internal class PushMetricController
    {
        private readonly TimeSpan pushInterval;
        private readonly Task worker;
        private MetricExporter metricExporter;
        private MetricProcessor metricProcessor;
        private Dictionary<MeterRegistryKey, MeterSdk> meters;

        public PushMetricController(
            Dictionary<MeterRegistryKey, MeterSdk> meters,
            MetricProcessor metricProcessor,
            MetricExporter metricExporter,
            TimeSpan pushInterval,
            CancellationTokenSource cts)
        {
            this.meters = meters;
            this.metricProcessor = metricProcessor;
            this.metricExporter = metricExporter;
            this.pushInterval = pushInterval;
            this.worker = Task.Factory.StartNew(
                s => this.Worker((CancellationToken)s), cts.Token);
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            IEnumerable<Metric<long>> longMetricToExport;
            IEnumerable<Metric<double>> doubleMetricToExport;

            await Task.Delay(this.pushInterval, cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    foreach (var meter in this.meters.Values)
                    {
                        meter.Collect();
                    }

                    OpenTelemetrySdkEventSource.Log.CollectionCompleted(sw.ElapsedMilliseconds);

                    // Collection is over at this point. All metrics are given
                    // to the MetricProcesor(Batcher).
                    // Let MetricProcessor know that this cycle is ending,
                    // and send the metrics from MetricProcessor
                    // to the MetricExporter.
                    this.metricProcessor.FinishCollectionCycle(out longMetricToExport, out doubleMetricToExport);

                    var longExportResult = await this.metricExporter.ExportAsync<long>(longMetricToExport, cancellationToken);
                    if (longExportResult != MetricExporter.ExportResult.Success)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricExporterErrorResult((int)longExportResult);

                        // we do not support retries for now and leave it up to exporter
                        // as only exporter implementation knows how to retry: which items failed
                        // and what is the reasonable policy for that exporter.
                    }

                    var doubleExportResult = await this.metricExporter.ExportAsync<double>(doubleMetricToExport, cancellationToken);
                    if (doubleExportResult != MetricExporter.ExportResult.Success)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricExporterErrorResult((int)doubleExportResult);

                        // we do not support retries for now and leave it up to exporter
                        // as only exporter implementation knows how to retry: which items failed
                        // and what is the reasonable policy for that exporter.
                    }
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.MetricControllerException(ex);
                }

                var remainingWait = this.pushInterval - sw.Elapsed;
                if (remainingWait > TimeSpan.Zero)
                {
                    await Task.Delay(remainingWait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
