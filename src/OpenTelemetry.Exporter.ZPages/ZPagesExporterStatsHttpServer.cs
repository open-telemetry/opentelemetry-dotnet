// <copyright file="ZPagesExporterStatsHttpServer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.ZPages.Implementation;
#if NET452
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// A HTTP listener used to expose ZPages stats.
    /// </summary>
    public class ZPagesExporterStatsHttpServer : IDisposable
    {
        private readonly HttpListener httpListener = new HttpListener();
        private readonly object syncObject = new object();

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporterStatsHttpServer"/> class.
        /// </summary>
        /// <param name="exporter">The <see cref="ZPagesExporterStatsHttpServer"/> instance.</param>
        public ZPagesExporterStatsHttpServer(ZPagesExporter exporter)
        {
            this.httpListener.Prefixes.Add(exporter?.Options?.Url ?? throw new ArgumentNullException(nameof(exporter)));
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the http server.</param>
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

                this.workerThread = Task.Factory.StartNew(this.WorkerThread, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
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

                    using Stream output = ctx.Response.OutputStream;
                    using var writer = new StreamWriter(output);
                    writer.WriteLine("<!DOCTYPE html>");
                    writer.WriteLine("<html><head><title>RPC Stats</title>" +
                                     "<meta charset=\"utf-8\">" +
                                     "<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.4.1/css/bootstrap.min.css\">" +
                                     "<script src=\"https://ajax.googleapis.com/ajax/libs/jquery/3.4.1/jquery.min.js\"></script>" +
                                     "<script src=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.4.1/js/bootstrap.min.js\"></script>" +
                                     "</head>");
                    writer.WriteLine("<body><div class=\"col-sm-1\"></div><div class=\"container col-sm-10\"><div class=\"jumbotron table-responsive\"><h1>RPC Stats</h2>" +
                                     "<table class=\"table table-bordered table-hover table-striped\">" +
                                     "<thead><tr><th>Span Name</th><th>Total Count</th><th>Count in last minute</th><th>Count in last hour</th><th>Average Latency (ms)</th>" +
                                     "<th>Average Latency in last minute (ms)</th><th>Average Latency in last hour (ms)</th><th>Total Errors</th><th>Errors in last minute</th><th>Errors in last minute</th><th>Last Updated</th></tr></thead>" +
                                     "<tbody>");

                    ConcurrentDictionary<string, ZPagesActivityAggregate> currentHourSpanList = ZPagesActivityTracker.CurrentHourList;
                    ConcurrentDictionary<string, ZPagesActivityAggregate> currentMinuteSpanList = ZPagesActivityTracker.CurrentMinuteList;

                    // Put span information in each row of the table
                    foreach (var spanName in currentHourSpanList.Keys)
                    {
                        long countInLastMinute = 0;
                        long countInLastHour = 0;
                        long averageLatencyInLastMinute = 0;
                        long averageLatencyInLastHour = 0;
                        long errorCountInLastMinute = 0;
                        long errorCountInLastHour = 0;

                        if (currentMinuteSpanList.ContainsKey(spanName))
                        {
                            currentMinuteSpanList.TryGetValue(spanName, out ZPagesActivityAggregate minuteSpanInformation);
                            countInLastMinute = minuteSpanInformation.EndedCount + ZPagesActivityTracker.ProcessingList[spanName];
                            averageLatencyInLastMinute = minuteSpanInformation.AvgLatencyTotal;
                            errorCountInLastMinute = minuteSpanInformation.ErrorCount;
                        }

                        currentHourSpanList.TryGetValue(spanName, out ZPagesActivityAggregate hourSpanInformation);
                        countInLastHour = hourSpanInformation.EndedCount + ZPagesActivityTracker.ProcessingList[spanName];
                        averageLatencyInLastHour = hourSpanInformation.AvgLatencyTotal;
                        errorCountInLastHour = hourSpanInformation.ErrorCount;

                        long totalAverageLatency = ZPagesActivityTracker.TotalLatency[spanName] / ZPagesActivityTracker.TotalEndedCount[spanName];

                        DateTimeOffset dateTimeOffset;

#if NET452
                        dateTimeOffset = DateTimeOffsetExtensions.FromUnixTimeMilliseconds(hourSpanInformation.LastUpdated);
#else
                        dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(hourSpanInformation.LastUpdated);
#endif

                        writer.WriteLine("<tr><td>" + hourSpanInformation.Name + "</td><td>" + ZPagesActivityTracker.TotalCount[spanName] + "</td><td>" + countInLastMinute + "</td><td>" + countInLastHour + "</td>" +
                                         "<td>" + totalAverageLatency + "</td><td>" + averageLatencyInLastMinute + "</td><td>" + averageLatencyInLastHour + "</td>" +
                                         "<td>" + ZPagesActivityTracker.TotalErrorCount[spanName] + "</td><td>" + errorCountInLastMinute + "</td><td>" + errorCountInLastHour + "</td><td>" + dateTimeOffset + " GMT" + "</td></tr>");
                    }

                    writer.WriteLine("</tbody></table>");
                    writer.WriteLine("</div></div></body></html>");
                }
            }
            catch (OperationCanceledException ex)
            {
                ZPagesExporterEventSource.Log.CanceledExport(ex);
            }
            catch (Exception ex)
            {
                ZPagesExporterEventSource.Log.FailedExport(ex);
            }
            finally
            {
                this.httpListener.Stop();
                this.httpListener.Close();
            }
        }
    }
}
