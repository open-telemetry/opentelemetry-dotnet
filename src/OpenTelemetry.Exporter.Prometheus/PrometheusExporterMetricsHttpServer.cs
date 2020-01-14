// <copyright file="PrometheusExporterMetricsHttpServer.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// A HTTP listener used to expose Prometheus metrics.
    /// </summary>
    public class PrometheusExporterMetricsHttpServer : IDisposable
    {
        private readonly PrometheusExporter exporter;
        private readonly CancellationToken token;
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterMetricsHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="PrometheusExporter"/> instance.</param>
        /// <param name="options">The <see cref="PrometheusExporterOptions"/> instance.</param>
        /// <param name="token">A <see cref="CancellationToken"/> that can be used to stop the Htto Server.</param>
        public PrometheusExporterMetricsHttpServer(PrometheusExporter exporter, PrometheusExporterOptions options, CancellationToken token)
        {
            this.exporter = exporter;
            this.token = token;
            this.httpListener.Prefixes.Add(options.Url);
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

                // link the passed in token if not null
                this.tokenSource = this.token == null ?
                    new CancellationTokenSource() :
                    CancellationTokenSource.CreateLinkedTokenSource(this.token);

                var token = this.tokenSource.Token;
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
            if (this.httpListener != null)
            {
                this.Stop();
            }
        }

        private void WorkerThread()
        {
            this.httpListener.Start();

            try
            {
                while (!this.token.IsCancellationRequested)
                {
                    var ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.token);

                    var ctx = ctxTask.Result;

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = PrometheusMetricBuilder.ContentType;

                    using (var output = ctx.Response.OutputStream)
                    {
                        using (var writer = new StreamWriter(output))
                        {
                            foreach (var metric in this.exporter.Metrics)
                            {
                                var labels = metric.Labels;
                                var value = metric.Value;

                                var builder = new PrometheusMetricBuilder()
                                    .WithName(metric.MetricName)
                                    .WithDescription(metric.MetricDescription);

                                builder = builder.WithType("counter");

                                foreach (var label in labels)
                                {
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder = metricValueBuilder.WithValue(value);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                }

                                builder.Write(writer);
                            }
                        }
                    }
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
