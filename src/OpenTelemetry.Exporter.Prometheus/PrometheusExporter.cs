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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Exporter of Open Telemetry traces and metrics to Prometheus.
    /// </summary>
    /// <typeparam name="T">The type of metric. Only long and double are supported now.</typeparam>
    public class PrometheusExporter<T> : MetricExporter<T>
        where T : struct
    {
        private readonly Metric<T> metric;

        private readonly PrometheusExporterOptions options;

        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;

        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter{T}"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        /// <param name="metric">The metric instance where metric values and labels can be read from.</param>
        public PrometheusExporter(PrometheusExporterOptions options, Metric<T> metric)
        {
            this.options = options;
            this.metric = metric;
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(Metric<T> metric, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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

                var metricsServer = new MetricsHttpServer<T>(this.metric, this.options, token);
                this.workerThread = Task.Factory.StartNew((Action)metricsServer.WorkerThread, TaskCreationOptions.LongRunning);
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
