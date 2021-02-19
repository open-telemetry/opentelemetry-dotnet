// <copyright file="WebServer.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

#pragma warning disable CS0618

namespace HttpServerExample
{
    public class WebServer
    {
        private MyLibrary library;

        private Meter meter;
        private MeasureMetric<long> duration;
        private CounterMetric<long> errorCount;
        private CounterMetric<long> incomingCount;
        private CounterMetric<long> outgoingCount;

        public WebServer()
        {
            // Initialize Web Server

            this.library = new MyLibrary();

            this.meter = MeterProvider.Default.GetMeter("MyServer", "1.0.0");

            // How to tell it what unit the measurements are in?
            // How to set the Description?
            this.duration = this.meter.CreateInt64Measure("Server.Duration", true);

            this.errorCount = this.meter.CreateInt64Counter("Server.Errors", true);

            this.incomingCount = this.meter.CreateInt64Counter("Server.Request.Incoming", true);

            this.outgoingCount = this.meter.CreateInt64Counter("Server.Request.Outgoing", true);
        }

        public void Shutdown()
        {
            // Shutdown
        }

        public Task StartServerTask(string prefix, CancellationToken token)
        {
            HttpListener listener = new HttpListener();

            listener.Prefixes.Add(prefix);

            Task serverTask = Task.Run(async () =>
            {
                Console.WriteLine("Server Started.");

                listener.Start();

                Stopwatch sw = new Stopwatch();

                while (!token.IsCancellationRequested)
                {
                    var contextTask = listener.GetContextAsync();

                    try
                    {
                        Task.WaitAny(new Task[] { contextTask }, token);
                    }
                    catch (Exception)
                    {
                        // Do Nothing
                    }

                    if (contextTask.IsCompletedSuccessfully && !contextTask.IsFaulted)
                    {
                        sw.Reset();

                        var context = await contextTask;
                        HttpListenerRequest request = context.Request;

                        var requestLabels = this.meter.GetLabelSet(new List<KeyValuePair<string, string>>()
                        {
                            KeyValuePair.Create("Host Name", this.library.GetHostName()),
                            KeyValuePair.Create("Process Id", this.library.GetProcessId().ToString()),
                            KeyValuePair.Create("Method", request.HttpMethod),
                            KeyValuePair.Create("Peer IP", request.RemoteEndPoint.Address.ToString()),
                            KeyValuePair.Create("Port", request.Url.Port.ToString()),
                        });

                        this.incomingCount.Add(default(SpanContext), 1, requestLabels);

                        // Parse request

                        var path = request.Url.AbsolutePath;

                        Console.WriteLine($"Server request for {path}");

                        // Format output

                        string responseString = $"<HTML><BODY>Hello world for {path}</BODY></HTML>";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                        HttpListenerResponse response = context.Response;
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 200;

                        // Return Response

                        System.IO.Stream output = response.OutputStream;
                        output.Write(buffer, 0, buffer.Length);
                        output.Close();

                        // ========

                        this.outgoingCount.Add(default(SpanContext), 1, requestLabels);

                        var elapsed = sw.ElapsedMilliseconds;
                        var labels = this.meter.GetLabelSet(new List<KeyValuePair<string, string>>()
                        {
                            KeyValuePair.Create("Host Name", this.library.GetHostName()),
                            KeyValuePair.Create("Process Id", this.library.GetProcessId().ToString()),
                            KeyValuePair.Create("Method", request.HttpMethod),
                            KeyValuePair.Create("Peer IP", request.RemoteEndPoint.Address.ToString()),
                            KeyValuePair.Create("Port", request.Url.Port.ToString()),

                            // Need to include Status Code
                            KeyValuePair.Create("Status Code", response.StatusCode.ToString()),
                        });
                        this.duration.Record(default(SpanContext), elapsed, labels);
                    }
                    else
                    {
                        // Count # of errors we have

                        var labels = this.meter.GetLabelSet(new List<KeyValuePair<string, string>>()
                        {
                            KeyValuePair.Create("Host Name", this.library.GetHostName()),
                            KeyValuePair.Create("Process Id", this.library.GetProcessId().ToString()),
                        });
                        this.errorCount.Add(default(SpanContext), 1, labels);
                    }
                }

                listener.Stop();

                Console.WriteLine("Server Stopped.");
            });

            return serverTask;
        }
    }
}
