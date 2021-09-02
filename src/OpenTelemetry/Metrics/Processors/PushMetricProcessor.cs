// <copyright file="PushMetricProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics
{
    public class PushMetricProcessor : MetricProcessor, IDisposable
    {
        private Task exportTask;
        private CancellationTokenSource token;
        private int exportIntervalMs;
        private Func<Batch<Metric>> getMetrics;
        private bool disposed;
        private AggregationTemporality aggTemporality;

        public PushMetricProcessor(BaseExporter<Metric> exporter, int exportIntervalMs, bool isDelta)
            : base(exporter)
        {
            this.exportIntervalMs = exportIntervalMs;
            this.token = new CancellationTokenSource();
            this.exportTask = new Task(() =>
            {
                while (!this.token.IsCancellationRequested)
                {
                    Task.Delay(this.exportIntervalMs).Wait();
                    this.Export();
                }
            });

            this.exportTask.Start();
            this.aggTemporality = isDelta ? AggregationTemporality.Delta : AggregationTemporality.Cumulative;
        }

        public override AggregationTemporality GetAggregationTemporality()
        {
            return this.aggTemporality;
        }

        public override void SetGetMetricFunction(Func<Batch<Metric>> getMetrics)
        {
            this.getMetrics = getMetrics;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !this.disposed)
            {
                try
                {
                    this.token.Cancel();
                    this.exporter.Dispose();
                    this.exportTask.Wait();
                }
                catch (Exception)
                {
                    // TODO: Log
                }

                this.disposed = true;
            }
        }

        private void Export()
        {
            if (this.getMetrics != null)
            {
                var metricsToExport = this.getMetrics();
                this.exporter.Export(metricsToExport);
            }
        }
    }
}
