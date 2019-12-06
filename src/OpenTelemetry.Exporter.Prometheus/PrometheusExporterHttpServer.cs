// <copyright file="PrometheusExporterHttpServer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// A Http host for <see cref="PrometheusExporter{T}"/> using a <see cref="HttpListener"/>.
    /// </summary>
    /// <typeparam name="T">The type of metric. Only long and double are supported now.</typeparam>
    public class PrometheusExporterHttpServer<T>
        where T : struct
    {
        private readonly Metric<T> metric;

        private readonly CancellationToken token;

        private readonly HttpListener httpListener = new HttpListener();

        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;

        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterHttpServer{T}"/> class.
        /// </summary>
        /// <param name="metric">The <see cref="Metric{T}"/> metric.</param>
        /// <param name="exporter">The <see cref="PrometheusExporter{T}"/> to host.</param>
        /// <param name="token">A <see cref="CancellationToken"/> that can be used to stop the worker thread.</param>
        public PrometheusExporterHttpServer(Metric<T> metric, PrometheusExporter<T> exporter, CancellationToken token)
        {
            this.metric = metric;
            this.token = token;
            this.httpListener.Prefixes.Add(exporter.Options.Url);
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
                            foreach (var metricSeries in this.metric.TimeSeries)
                            {
                                var labels = metricSeries.Key.Labels;
                                var values = metricSeries.Value.Points;

                                var builder = new PrometheusMetricBuilder()
                                    .WithName(this.metric.MetricName)
                                    .WithDescription(this.metric.MetricDescription);

                                builder = builder.WithType("counter");

                                foreach (var label in labels)
                                {
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder = metricValueBuilder.WithValue((long)(object)values[0]);
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
