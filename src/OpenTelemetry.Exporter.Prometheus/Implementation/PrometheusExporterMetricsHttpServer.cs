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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// A HTTP listener used to expose Prometheus metrics.
    /// </summary>
    internal sealed class PrometheusExporterMetricsHttpServer : IDisposable
    {
        private readonly PrometheusExporter exporter;
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object syncObject = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterMetricsHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="PrometheusExporter"/> instance.</param>
        public PrometheusExporterMetricsHttpServer(PrometheusExporter exporter)
        {
            Guard.Null(exporter, nameof(exporter));

            this.exporter = exporter;
            if ((exporter.Options.HttpListenerPrefixes?.Count ?? 0) <= 0)
            {
                throw new ArgumentException("No HttpListenerPrefixes were specified on PrometheusExporterOptions.");
            }

            string path = exporter.Options.ScrapeEndpointPath ?? PrometheusExporterOptions.DefaultScrapeEndpointPath;
            if (!path.StartsWith("/"))
            {
                path = $"/{path}";
            }

            if (!path.EndsWith("/"))
            {
                path = $"{path}/";
            }

            foreach (string prefix in exporter.Options.HttpListenerPrefixes)
            {
                this.httpListener.Prefixes.Add($"{prefix.TrimEnd('/')}{path}");
            }
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the htto server.</param>
        public void Start(CancellationToken token = default)
        {
            lock (this.syncObject)
            {
                if (this.tokenSource != null)
                {
                    return;
                }

                // link the passed in token if not null
                this.tokenSource = token == default ?
                    new CancellationTokenSource() :
                    CancellationTokenSource.CreateLinkedTokenSource(token);

                this.workerThread = Task.Factory.StartNew(this.WorkerProc, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Stop exporter.
        /// </summary>
        public void Stop()
        {
            lock (this.syncObject)
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

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.httpListener != null && this.httpListener.IsListening)
            {
                this.Stop();
                this.httpListener.Close();
            }
        }

        private void WorkerProc()
        {
            var bufferSize = 85000; // encourage the object to live in LOH (large object heap)
            var buffer = new byte[bufferSize];

            this.httpListener.Start();

            try
            {
                using var scope = SuppressInstrumentationScope.Begin();
                while (!this.tokenSource.IsCancellationRequested)
                {
                    var ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.tokenSource.Token);
                    var ctx = ctxTask.Result;

                    try
                    {
                        ctx.Response.StatusCode = 200;
                        ctx.Response.Headers.Add("Server", string.Empty);
                        ctx.Response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

                        this.exporter.OnExport = (metrics) =>
                        {
                            try
                            {
                                var cursor = 0;
                                foreach (var metric in metrics)
                                {
                                    while (true)
                                    {
                                        try
                                        {
                                            cursor = PrometheusSerializer.WriteMetric(buffer, cursor, metric);
                                            break;
                                        }
                                        catch (IndexOutOfRangeException)
                                        {
                                            bufferSize = bufferSize * 2;

                                            // there are two cases we might run into the following condition:
                                            // 1. we have many metrics to be exported - in this case we probably want
                                            //    to put some upper limit and allow the user to configure it.
                                            // 2. we got an IndexOutOfRangeException which was triggered by some other
                                            //    code instead of the buffer[cursor++] - in this case we should give up
                                            //    at certain point rather than allocating like crazy.
                                            if (bufferSize > 100 * 1024 * 1024)
                                            {
                                                throw;
                                            }

                                            var newBuffer = new byte[bufferSize];
                                            buffer.CopyTo(newBuffer, 0);
                                            buffer = newBuffer;
                                        }
                                    }
                                }

                                ctx.Response.OutputStream.Write(buffer, 0, cursor - 0);
                                return ExportResult.Success;
                            }
                            catch (Exception)
                            {
                                return ExportResult.Failure;
                            }
                        };

                        this.exporter.Collect(Timeout.Infinite);
                        this.exporter.OnExport = null;
                    }
                    catch (Exception ex)
                    {
                        PrometheusExporterEventSource.Log.FailedExport(ex);

                        ctx.Response.StatusCode = 500;
                    }

                    try
                    {
                        ctx.Response.Close();
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                PrometheusExporterEventSource.Log.CanceledExport(ex);
            }
            finally
            {
                try
                {
                    this.httpListener.Stop();
                    this.httpListener.Close();
                }
                catch (Exception exFromFinally)
                {
                    PrometheusExporterEventSource.Log.FailedShutdown(exFromFinally);
                }
            }
        }
    }
}
