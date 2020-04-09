﻿// <copyright file="TestServer.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Collector.Dependencies.Tests
{
    public class TestServer
    {
        private static readonly Random GlobalRandom = new Random();

        private class RunningServer : IDisposable
        {
            private readonly Task httpListenerTask;
            private readonly HttpListener listener;
            private readonly AutoResetEvent initialized = new AutoResetEvent(false);

            public RunningServer(Action<HttpListenerContext> action, string host, int port)
            {
                listener = new HttpListener();

                listener.Prefixes.Add($"http://{host}:{port}/");
                listener.Start();

                httpListenerTask = new Task(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var ctxTask = listener.GetContextAsync();

                            initialized.Set();

                            action(await ctxTask.ConfigureAwait(false));
                        }
                        catch (Exception ex)
                        {
                            if (ex is ObjectDisposedException // Listener was closed before we got into GetContextAsync.
                                || (ex is HttpListenerException httpEx && httpEx.ErrorCode == 995)) // Listener was closed while we were in GetContextAsync.
                            {
                                break;
                            }
                            Assert.True(false, ex.ToString());
                        }
                    }
                });
            }

            public void Start()
            {
                httpListenerTask.Start();
                initialized.WaitOne();
            }

            public void Dispose()
            {
                try
                {
                    listener?.Stop();
                }
                catch (ObjectDisposedException)
                {
                    // swallow this exception just in case
                }
            }
        }

        public static IDisposable RunServer(Action<HttpListenerContext> action, out string host, out int port)
        {
            host = "localhost";
            port = 0;
            RunningServer server = null;

            var retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    port = GlobalRandom.Next(2000, 5000);
                    server = new RunningServer(action, host, port);
                    server.Start();
                    break;
                }
                catch (HttpListenerException)
                {
                    retryCount--;
                }
            }

            return server;
        }
    }
}
