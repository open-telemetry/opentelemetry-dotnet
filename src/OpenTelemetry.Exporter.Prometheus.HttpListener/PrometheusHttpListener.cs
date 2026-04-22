// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class PrometheusHttpListener : IDisposable
{
    private readonly PrometheusExporter exporter;
    private readonly HttpListener httpListener = new();
    private readonly Lock syncObject = new();

    private CancellationTokenSource? tokenSource;
    private Task? workerThread;

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

        var path = options.ScrapeEndpointPath ?? PrometheusHttpListenerOptions.DefaultScrapeEndpointPath;

#if NET
        if (!path.StartsWith('/'))
#else
        if (!path.StartsWith("/", StringComparison.Ordinal))
#endif
        {
            path = $"/{path}";
        }

#if NET
        if (!path.EndsWith('/'))
#else
        if (!path.EndsWith("/", StringComparison.Ordinal))
#endif
        {
            path = $"{path}/";
        }

        IEnumerable<string> prefixesToUse;
        if (!options.UriPrefixesExplicitlySet)
        {
            var uriBuilder = new UriBuilder(Uri.UriSchemeHttp, options.Host, options.Port);
            prefixesToUse = new[] { uriBuilder.Uri.AbsoluteUri };
        }
        else
        {
            // TODO: Remove this branch (along with UriPrefixesExplicitlySet, the
            // obsolete UriPrefixes property, and this pragma) prior to the stable
            // release. Kept during the prerelease transition window so existing
            // consumers of UriPrefixes continue to work.
            // Tracking issue: https://github.com/open-telemetry/opentelemetry-dotnet/issues/7107
#pragma warning disable CS0618 // Type or member is obsolete
            prefixesToUse = options.UriPrefixes;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        foreach (var uriPrefix in prefixesToUse)
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

            this.httpListener.Start();

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
        CancellationTokenSource? tokenSource;
        Task? workerThread;

        lock (this.syncObject)
        {
            tokenSource = this.tokenSource;
            workerThread = this.workerThread;

            if (tokenSource == null)
            {
                return;
            }

            this.tokenSource = null;
            this.workerThread = null;
        }

        try
        {
            tokenSource.Cancel();
            workerThread?.Wait();
        }
        finally
        {
            tokenSource.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Stop();

        if (this.httpListener.IsListening)
        {
            this.httpListener.Close();
        }
    }

    private static bool AcceptsOpenMetrics(HttpListenerRequest request)
    {
        var acceptHeader = request.Headers["Accept"];

        return !string.IsNullOrEmpty(acceptHeader) && PrometheusHeadersParser.AcceptsOpenMetrics(acceptHeader);
    }

    private void WorkerProc()
    {
        var cancellationToken = this.tokenSource!.Token;

        try
        {
            using var scope = SuppressInstrumentationScope.Begin();
            while (!cancellationToken.IsCancellationRequested)
            {
                var ctxTask = this.httpListener.GetContextAsync();
                ctxTask.Wait(cancellationToken);
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
            var openMetricsRequested = AcceptsOpenMetrics(context.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested).ConfigureAwait(false);

            try
            {
                context.Response.Headers.Add("Server", string.Empty);

                var dataView = openMetricsRequested ? collectionResponse.OpenMetricsView : collectionResponse.PlainTextView;

                if (dataView.Count > 0)
                {
                    context.Response.StatusCode = 200;
                    context.Response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
                    context.Response.ContentType = openMetricsRequested
                        ? "application/openmetrics-text; version=1.0.0; charset=utf-8"
                        : "text/plain; charset=utf-8; version=0.0.4";

#if NET
                    await context.Response.OutputStream.WriteAsync(dataView.Array.AsMemory(0, dataView.Count)).ConfigureAwait(false);
#else
                    await context.Response.OutputStream.WriteAsync(dataView.Array, 0, dataView.Count).ConfigureAwait(false);
#endif
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
}
