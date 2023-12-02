// <copyright file="PrometheusHttpListener.cs" company="OpenTelemetry Authors">
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

using System.Net;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class PrometheusHttpListener : IDisposable
{
    private const string OpenMetricsMediaType = "application/openmetrics-text";

    private readonly PrometheusExporter exporter;
    private readonly HttpListener httpListener = new();
    private readonly object syncObject = new();

    private CancellationTokenSource tokenSource;
    private Task workerThread;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusHttpListener"/> class.
    /// </summary>
    /// <param name="exporter"><see cref="PrometheusExporter"/>The exporter instance.</param>
    /// <param name="options"><see cref="PrometheusHttpListenerOptions"/>The configured HttpListener options.</param>
    public PrometheusHttpListener(PrometheusExporter exporter, PrometheusHttpListenerOptions options)
    {
        Guard.ThrowIfNull(exporter);
        Guard.ThrowIfNull(options);

        this.exporter = exporter;

        string path = options.ScrapeEndpointPath;

        if (!path.StartsWith("/"))
        {
            path = $"/{path}";
        }

        if (!path.EndsWith("/"))
        {
            path = $"{path}/";
        }

        foreach (string uriPrefix in options.UriPrefixes)
        {
            this.httpListener.Prefixes.Add($"{uriPrefix.TrimEnd('/')}{path}");
        }
    }

    /// <summary>
    /// Start the HttpListener.
    /// </summary>
    /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the HTTP listener.</param>
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
    /// Gracefully stop the PrometheusHttpListener.
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
        this.Stop();

        if (this.httpListener != null && this.httpListener.IsListening)
        {
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
            var openMetricsRequested = this.AcceptsOpenMetrics(context.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested).ConfigureAwait(false);

            try
            {
                context.Response.Headers.Add("Server", string.Empty);
                if (collectionResponse.View.Count > 0)
                {
                    context.Response.StatusCode = 200;
                    context.Response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
                    context.Response.ContentType = openMetricsRequested
                        ? "application/openmetrics-text; version=1.0.0; charset=utf-8"
                        : "text/plain; charset=utf-8; version=0.0.4";

                    await context.Response.OutputStream.WriteAsync(collectionResponse.View.Array, 0, collectionResponse.View.Count).ConfigureAwait(false);
                }
                else
                {
                    // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
                    context.Response.StatusCode = 200;
                    PrometheusExporterEventSource.Log.NoMetrics();
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

    private bool AcceptsOpenMetrics(HttpListenerRequest request)
    {
        var requestAccept = request.Headers["Accept"];

        if (string.IsNullOrEmpty(requestAccept))
        {
            return false;
        }

        var acceptTypes = requestAccept.Split(',');

        foreach (var acceptType in acceptTypes)
        {
            var acceptSubType = acceptType.Split(';').FirstOrDefault()?.Trim();

            if (acceptSubType == OpenMetricsMediaType)
            {
                return true;
            }
        }

        return false;
    }
}
