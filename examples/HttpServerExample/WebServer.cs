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
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HttpServerExample
{
    public class WebServer
    {
        public WebServer()
        {
            // Initialize Web Server
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

                    if (contextTask.IsCompletedSuccessfully)
                    {
                        var context = await contextTask;
                        HttpListenerRequest request = context.Request;

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
                    }
                }

                listener.Stop();

                Console.WriteLine("Server Stopped.");
            });

            return serverTask;
        }
    }
}
