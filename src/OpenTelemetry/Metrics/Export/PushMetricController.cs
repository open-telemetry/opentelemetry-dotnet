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

namespace OpenTelemetry.Metrics.Export
{
    public class PushMetricController
    {
        private readonly TimeSpan pushInterval;
        private readonly Task worker;
        private MetricExporter metricExporter;
        private MeterFactory meterFactory;

        public PushMetricController(MeterFactory meterFactory,
            MetricExporter metricExporter,
            TimeSpan pushInterval,
            CancellationTokenSource cts)
        {
            this.meterFactory = meterFactory;
            this.metricExporter = metricExporter;
            this.pushInterval = pushInterval;
            this.worker = Task.Factory.StartNew(
                s => this.Worker((CancellationToken)s), cts.Token);
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            try
            {
                List<Metric<long>> longMetricToExport = new List<Metric<long>>();
                List<Metric<double>> doubleMetricToExport = new List<Metric<double>>();

                await Task.Delay(this.pushInterval, cancellationToken).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var sw = Stopwatch.StartNew();

                    foreach (var meter in this.meterFactory.GetAllMeters())
                    {                        
                        var metricsToExportTuple = (meter as MeterSdk).Collect();
                        if (metricsToExportTuple.Item1 != null)
                        {
                            longMetricToExport.AddRange(metricsToExportTuple.Item1);
                        }

                        if (metricsToExportTuple.Item2 != null)
                        {
                            doubleMetricToExport.AddRange(metricsToExportTuple.Item2);
                        }
                    }

                    OpenTelemetrySdkEventSource.Log.CollectionCompleted(sw.ElapsedMilliseconds);

                    var longExportResult = await this.metricExporter.ExportAsync<long>(longMetricToExport, cancellationToken);
                    var doubleExportResult = await this.metricExporter.ExportAsync<double>(doubleMetricToExport, cancellationToken);

                    var remainingWait = this.pushInterval - sw.Elapsed;
                    if (remainingWait > TimeSpan.Zero)
                    {
                        await Task.Delay(remainingWait, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.MetricControllerException(ex);
            }
        }
    }
}
