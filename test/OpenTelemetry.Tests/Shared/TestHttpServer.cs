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

        private volatile bool disposing;

        public RunningServer(Action<HttpListenerContext> action, string host, int port)
        {
            this.listener = new HttpListener();

            this.listener.Prefixes.Add($"http://{host}:{port}/");
            this.listener.Start();

            this.httpListenerTask = this.RunAsync(action);
        }

        public void Start()
            => this.initialized.WaitOne();

        public void Dispose()
        {
            try
            {
                this.disposing = true;
                this.listener.Close();
                this.httpListenerTask?.Wait();
                this.initialized.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // swallow this exception just in case
            }
        }

        private async Task RunAsync(Action<HttpListenerContext> action)
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
                catch (Exception) when (this.disposing)
                {
                    // The listener was closed before we got into GetContextAsync, or while we
                    // were in it, or between handling a request and re-entering it. What that
                    // throws is platform- and timing-dependent: observed variants include
                    // ObjectDisposedException, HttpListenerException with a Win32 error of 995
                    // (ERROR_OPERATION_ABORTED) on .NET or 1 (ERROR_INVALID_FUNCTION) on .NET
                    // Framework, InvalidOperationException because the listener is no longer
                    // started, and even ApplicationException wrapping E_HANDLE if Close() races
                    // with (Begin/End)GetContextAsync binding the listener's OS handle to the
                    // I/O completion port. Rather than growing this list of exception types
                    // every time CI finds a new one, key off `disposing` alone: once Dispose()
                    // has asked the listener to stop (set before it calls Close(), so this can't
                    // race with that call the way checking `listener.IsListening` did), any
                    // exception from this loop is expected shutdown noise, not a real failure.
                    return;
                }
            }
        }
    }
}
