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

namespace OpenTelemetry.Exporter.Prometheus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Exporter.Prometheus.Implementation;
    using OpenTelemetry.Stats;

    /// <summary>
    /// Exporter of Open Telemetry traces and metrics to Azure Application Insights.
    /// </summary>
    public class PrometheusExporter
    {
        private readonly IViewManager viewManager;

        private readonly PrometheusExporterOptions options;

        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;

        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        /// <param name="viewManager">View manager to get stats from.</param>
        public PrometheusExporter(PrometheusExporterOptions options, IViewManager viewManager)
        {
            this.options = options;
            this.viewManager = viewManager;
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

                var metricsServer = new MetricsHttpServer(this.viewManager, this.options, token);
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
