// <copyright file="ZPagesExporterStatsHttpServer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// A HTTP listener used to expose ZPages stats.
    /// </summary>
    public class ZPagesExporterStatsHttpServer : IDisposable
    {
        private readonly ZPagesExporter exporter;
        private readonly SimpleSpanProcessor spanProcessor;
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object lck = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporterStatsHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="ZPagesExporterStatsHttpServer"/> instance.</param>
        /// <param name="spanProcessor">The <see cref="SimpleSpanProcessor"/> instance.</param>
        public ZPagesExporterStatsHttpServer(ZPagesExporter exporter, SimpleSpanProcessor spanProcessor)
        {
            this.exporter = exporter;
            this.spanProcessor = spanProcessor;
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
                    ctx.Response.ContentType = ZPagesStatsBuilder.ContentType;

                    using (var output = ctx.Response.OutputStream)
                    {
                        using (var writer = new StreamWriter(output))
                        {
                            writer.WriteLine("Span Count : " + this.spanProcessor.GetSpanCount());
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
