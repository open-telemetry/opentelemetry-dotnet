﻿// <copyright file="ZPagesExporterStatsHttpServer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
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
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the http server.</param>
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
                    Task<HttpListenerContext> ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.tokenSource.Token);

                    var ctx = ctxTask.Result;

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = ZPagesStatsBuilder.ContentType;

                    using (Stream output = ctx.Response.OutputStream)
                    {
                        using (var writer = new StreamWriter(output))
                        {
                            writer.WriteLine("<!DOCTYPE html>");
                            writer.WriteLine("<html><head><title>RPC Stats</title>" +
                                             "<meta charset=\"utf-8\">" +
                                             "<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.4.1/css/bootstrap.min.css\">" +
                                             "<script src=\"https://ajax.googleapis.com/ajax/libs/jquery/3.4.1/jquery.min.js\"></script>" +
                                             "<script src=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.4.1/js/bootstrap.min.js\"></script></head>");
                            writer.WriteLine("<body><div class=\"col-sm-1\"></div><div class=\"container col-sm-10\"><div class=\"jumbotron table-responsive\"><h1>RPC Stats</h2>" +
                                             "<table class=\"table table-bordered table-hover table-striped\">" +
                                             "<thead style=\"color: white;background-color: Teal;\"><tr><th>Span Name</th><th>Total Count</th><th>Count in last minute</th><th>Count in last hour</th><th>Average Latency</th>" +
                                             "<th>Average Latency in last minute</th><th>Average Latency in last hour</th><th>Total Errors</th><th>Errors in last minute</th><th>Errors in last minute</th><th>Last Updated</th></tr></thead>" +
                                             "<tbody style=\"background-color: white;\">");

                            Dictionary<string, ZPagesSpanInformation> spanList = this.exporter.GetSpanList();

                            // Put span information in each row of the table
                            foreach (var spanName in spanList.Keys)
                            {
                                ZPagesSpanInformation spanInformation = new ZPagesSpanInformation();
                                spanList.TryGetValue(spanName, out spanInformation);
                                writer.WriteLine("<tr><td>" + spanInformation.Name + "</td><td>" + spanInformation.CountTotal + "</td><td>" + spanInformation.CountMinute + "</td><td>" + spanInformation.CountHour + "</td>" +
                                                 "<td>" + spanInformation.AvgLatencyTotal + "</td><td>" + spanInformation.AvgLatencyMinute + "</td><td>" + spanInformation.AvgLatencyHour + "</td>" +
                                                 "<td>" + spanInformation.ErrorTotal + "</td><td>" + spanInformation.ErrorMinute + "</td><td>" + spanInformation.ErrorHour + "</td><td>" + DateTimeOffset.FromUnixTimeMilliseconds(spanInformation.LastUpdated) + " GMT" + "</td></tr>");
                            }

                            writer.WriteLine("</tbody></table>");
                            writer.WriteLine("</div></div></body></html>");
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
