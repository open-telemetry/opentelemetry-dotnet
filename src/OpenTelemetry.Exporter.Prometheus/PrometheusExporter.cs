// <copyright file="PrometheusExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Exporter of Open Telemetry metrics to Prometheus.
    /// </summary>
    public class PrometheusExporter : MetricExporter
    {
        private readonly PrometheusExporterOptions options;

        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;
        private List<Metric> metrics;
        private MetricsHttpServer metricsHttpServer;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public PrometheusExporter(PrometheusExporterOptions options)
        {
            this.options = options;
            this.metrics = new List<Metric>();
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(List<Metric> metrics, CancellationToken cancellationToken)
        {
            this.metricsHttpServer.Metrics = metrics;
            return Task.FromResult(ExportResult.Success);
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        public void Start()
        {
            lock (this.lck)
            {
                if (this.tokenSource != null)
                {
                    return;
                }

                this.tokenSource = new CancellationTokenSource();

                var token = this.tokenSource.Token;

                this.metricsHttpServer = new MetricsHttpServer(this.metrics, this.options, token);
                this.workerThread = Task.Factory.StartNew((Action)this.metricsHttpServer.WorkerThread, TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Stop exporter.
        /// </summary>
        public void Stop()
        {
            lock (this.lck)
            {
                if (this.tokenSource == null)
                {
                    return;
                }

                this.tokenSource.Cancel();
                this.workerThread.Wait();
                this.tokenSource = null;
            }
        }
    }
}
