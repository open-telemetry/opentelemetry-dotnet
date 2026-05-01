// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace OpenTelemetry.Tests;

internal static class TestHttpServer
{
    public static IDisposable RunServer(Action<HttpListenerContext> action, out string host, out int port)
    {
        host = "localhost";
        port = 0;
        RunningServer? server = null;

        var retryCount = 5;
        var remainingAttempts = retryCount;

        while (remainingAttempts > 0)
        {
            try
            {
                port = TcpPortProvider.GetOpenPort();
                server = new RunningServer(action, host, port);
                server.Start();
                break;
            }
            catch (HttpListenerException)
            {
                server?.Dispose();
                server = null;
                remainingAttempts--;
            }
        }

        return server ?? throw new InvalidOperationException($"Server could not be started within {retryCount} attempts.");
    }

    private sealed class RunningServer : IDisposable
    {
        private readonly Task httpListenerTask;
        private readonly HttpListener listener;
        private readonly AutoResetEvent initialized = new(false);

        public RunningServer(Action<HttpListenerContext> action, string host, int port)
        {
            this.listener = new HttpListener();

            this.listener.Prefixes.Add($"http://{host}:{port}/");
            this.listener.Start();

            this.httpListenerTask = new Task(async () =>
            {
                while (true)
                {
                    try
                    {
                        var ctxTask = this.listener.GetContextAsync();

                        this.initialized.Set();

#pragma warning disable CA2007 // Do not directly await a Task
                        action(await ctxTask);
#pragma warning disable CA2007 // Do not directly await a Task
                    }
                    catch (Exception ex)
                    {
                        if (ex is ObjectDisposedException
                            || (ex is HttpListenerException httpEx && httpEx.ErrorCode == 995))
                        {
                            // Listener was closed before we got into GetContextAsync or
                            // Listener was closed while we were in GetContextAsync.
                            break;
                        }

                        throw;
                    }
                }
            });
        }

        public void Start()
        {
            this.httpListenerTask.Start();
            this.initialized.WaitOne();
        }

        public void Dispose()
        {
            try
            {
                this.listener.Close();
                this.httpListenerTask?.Wait();
                this.initialized.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // swallow this exception just in case
            }
        }
    }
}
