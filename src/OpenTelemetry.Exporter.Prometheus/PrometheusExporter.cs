// <copyright file="PrometheusExporter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter of OpenTelemetry metrics to Prometheus.
    /// </summary>
    [AggregationTemporality(AggregationTemporality.Cumulative)]
    [ExportModes(ExportModes.Pull)]
    public class PrometheusExporter : BaseExporter<Metric>, IPullMetricExporter
    {
        internal const string HttpListenerStartFailureExceptionMessage = "PrometheusExporter http listener could not be started.";
        internal readonly PrometheusExporterOptions Options;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly PrometheusExporterMetricsHttpServer metricsHttpServer;
        private Func<int, Task<bool>> funcCollectAndPullAsync;
        private Func<Batch<Metric>, Task<ExportResult>> funcPullExportAsync;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public PrometheusExporter(PrometheusExporterOptions options)
        {
            this.Options = options;

            if (options.StartHttpListener)
            {
                try
                {
                    this.metricsHttpServer = new PrometheusExporterMetricsHttpServer(this);
                    this.metricsHttpServer.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(HttpListenerStartFailureExceptionMessage, ex);
                }
            }
        }

        public Func<int, Task<bool>> CollectAndPullAsync
        {
            get => this.funcCollectAndPullAsync;
            set => this.funcCollectAndPullAsync = value;
        }

        internal Func<Batch<Metric>, Task<ExportResult>> OnPullExportAsync
        {
            get => this.funcPullExportAsync;
            set => this.funcPullExportAsync = value;
        }

        public override ExportResult Export(in Batch<Metric> metrics)
        {
            // todo: Implement PUSH export here.
            throw new NotImplementedException();
        }

        public Task<ExportResult> PullAsync(Batch<Metric> metrics)
        {
            var pullFunc = this.funcPullExportAsync;
            if (pullFunc == null)
            {
                // todo: Log an error.
                return Task.FromResult(ExportResult.Failure);
            }

            return pullFunc(metrics);
        }

        internal bool TryEnterSemaphore()
        {
            return this.semaphore.Wait(0);
        }

        internal void ReleaseSemaphore()
        {
            this.semaphore.Release();
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.metricsHttpServer?.Dispose();
                    this.semaphore.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
