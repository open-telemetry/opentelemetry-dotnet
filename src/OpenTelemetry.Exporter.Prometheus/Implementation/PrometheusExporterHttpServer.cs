// <copyright file="PrometheusExporterHttpServer.cs" company="OpenTelemetry Authors">
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
    /// An HTTP listener used to expose Prometheus metrics.
    /// </summary>
    internal sealed class PrometheusExporterHttpServer : IDisposable
    {
        private readonly PrometheusExporter exporter;
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object syncObject = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="PrometheusExporter"/> instance.</param>
        public PrometheusExporterHttpServer(PrometheusExporter exporter)
        {
            Guard.ThrowIfNull(exporter, nameof(exporter));

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
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the HTTP server.</param>
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
            this.httpListener.Start();

            try
            {
                using var scope = SuppressInstrumentationScope.Begin();
                while (!this.tokenSource.IsCancellationRequested)
                {
                    var ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.tokenSource.Token);
                    var ctx = ctxTask.Result;

                    Task.Run(() => this.ProcessRequestAsync(ctx));
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

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var collectionResponse = await this.exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                try
                {
                    if (collectionResponse.View.Count > 0)
                    {
                        context.Response.StatusCode = 200;
                        context.Response.Headers.Add("Server", string.Empty);
                        context.Response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
                        context.Response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

                        await context.Response.OutputStream.WriteAsync(collectionResponse.View.Array, 0, collectionResponse.View.Count).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new InvalidOperationException("Collection failure.");
                    }
                }
                finally
                {
                    this.exporter.CollectionManager.ExitCollect();
                }
            }
            catch (Exception ex)
            {
                PrometheusExporterEventSource.Log.FailedExport(ex);

                context.Response.StatusCode = 500;
            }

            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }
    }
}
