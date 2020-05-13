// <copyright file="PrometheusExporterMetricsHttpServer.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// A HTTP listener used to expose Prometheus metrics.
    /// </summary>
    public class PrometheusExporterMetricsHttpServer : IDisposable
    {
        private readonly PrometheusExporter exporter;
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterMetricsHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="PrometheusExporter"/> instance.</param>
        public PrometheusExporterMetricsHttpServer(PrometheusExporter exporter)
        {
            this.exporter = exporter;
            this.httpListener.Prefixes.Add(exporter.Options.Url);
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the htto server.</param>
        public void Start(CancellationToken token = default)
        {
            lock (this.lck)
            {
                if (this.tokenSource != null)
                {
                    return;
                }

                // link the passed in token if not null
                this.tokenSource = token == default ?
                    new CancellationTokenSource() :
                    CancellationTokenSource.CreateLinkedTokenSource(token);

                this.workerThread = Task.Factory.StartNew((Action)this.WorkerThread, TaskCreationOptions.LongRunning);
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

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        public void Dispose()
        {
            if (this.httpListener != null && this.httpListener.IsListening)
            {
                this.Stop();
            }
        }

        private void WorkerThread()
        {
            this.httpListener.Start();

            try
            {
                while (!this.tokenSource.IsCancellationRequested)
                {
                    var ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.tokenSource.Token);

                    var ctx = ctxTask.Result;

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = PrometheusMetricBuilder.ContentType;

                    using var output = ctx.Response.OutputStream;
                    using var writer = new StreamWriter(output);
                    this.exporter.WriteMetricsCollection(writer);
                }
            }
            catch (OperationCanceledException)
            {
                // this will happen when cancellation will be requested
            }
            catch (Exception)
            {
                // TODO: report error
            }
            finally
            {
                this.httpListener.Stop();
                this.httpListener.Close();
            }
        }
    }
}
