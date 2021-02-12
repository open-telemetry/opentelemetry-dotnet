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
        private CancellationTokenSource tokenSrc = new CancellationTokenSource();

        private Task serverTask;

        private HttpListener listener = new HttpListener();

        public WebServer()
        {
            this.listener.Prefixes.Add("http://127.0.0.1:3000/");
        }

        public void Start()
        {
            var token = this.tokenSrc.Token;

            this.serverTask = Task.Run(async () =>
                {
                Console.WriteLine("Server Started.");

                this.listener.Start();

                while (!token.IsCancellationRequested)
                {
                    var contextTask = this.listener.GetContextAsync();

                    Task.WaitAny(new Task[] { contextTask }, token);
                    if (contextTask.IsCompletedSuccessfully)
                    {
                        var context = await contextTask;
                        HttpListenerRequest request = context.Request;

                        // Parse request

                        var url = request.Url;
                        string responseString = $"<HTML><BODY>Hello world! {url.AbsolutePath}</BODY></HTML>";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                        // Format output

                        HttpListenerResponse response = context.Response;
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 200;

                        System.IO.Stream output = response.OutputStream;
                        output.Write(buffer, 0, buffer.Length);
                        output.Close();
                    }
                }

                this.listener.Stop();

                Console.WriteLine("Server Stopped.");
            });
        }

        public void Stop()
        {
            this.tokenSrc.Cancel();

            try
            {
                this.serverTask.Wait();
            }
            catch (Exception)
            {
                // Do Nothing
            }
        }
    }
}
