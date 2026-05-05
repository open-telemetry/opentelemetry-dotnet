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

    private volatile bool disposed;
    private int activeRequestCount;
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

        if (!options.UriPrefixesExplicitlySet)
        {
            var uriBuilder = new UriBuilder(Uri.UriSchemeHttp, options.Host, options.Port) { Path = path };
            this.httpListener.Prefixes.Add(uriBuilder.Uri.AbsoluteUri);
        }
        else
        {
            // TODO: Remove this branch (along with UriPrefixesExplicitlySet, the
            // obsolete UriPrefixes property, and this pragma) prior to the stable
            // release. Kept during the prerelease transition window so existing
            // consumers of UriPrefixes continue to work.
            // Tracking issue: https://github.com/open-telemetry/opentelemetry-dotnet/issues/7107
#pragma warning disable CS0618 // Type or member is obsolete
            foreach (var uriPrefix in options.UriPrefixes)
            {
                this.httpListener.Prefixes.Add($"{uriPrefix.TrimEnd('/')}{path}");
            }
#pragma warning restore CS0618 // Type or member is obsolete
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
#if NET
            ObjectDisposedException.ThrowIf(this.disposed, this);
#else
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(PrometheusHttpListener));
            }
#endif

            if (this.tokenSource != null)
            {
                return;
            }

            this.httpListener.Start();

            // link the passed in token if not null
            this.tokenSource = token == CancellationToken.None ?
                new CancellationTokenSource() :
                CancellationTokenSource.CreateLinkedTokenSource(token);

            var workerToken = this.tokenSource.Token;
            this.workerThread = Task.Factory.StartNew(
                (paramToken) => this.ProcessingLoopAsync((CancellationToken)paramToken!),
                workerToken,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.syncObject)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.Stop();

        // Wait for in-flight requests to finish (they will observe the
        // cancelled token and return 503 quickly). Use a timeout to avoid
        // blocking indefinitely if a request is unexpectedly stuck.
        SpinWait.SpinUntil(() => Volatile.Read(ref this.activeRequestCount) == 0, TimeSpan.FromSeconds(5));

        try
        {
            this.httpListener.Stop();
            this.httpListener.Close();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException)
        {
        }
    }

    private static bool AcceptsOpenMetrics(HttpListenerRequest request)
    {
        var acceptHeader = request.Headers["Accept"];

        return !string.IsNullOrEmpty(acceptHeader) && PrometheusHeadersParser.AcceptsOpenMetrics(acceptHeader);
    }

    /// <summary>
    /// Gracefully stop the PrometheusHttpListener.
    /// </summary>
    private void Stop()
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

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = SuppressInstrumentationScope.Begin();
            while (!cancellationToken.IsCancellationRequested)
            {
#if NET
                var context = await this.httpListener
                    .GetContextAsync()
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
#else
                var task = this.httpListener.GetContextAsync();
                task.Wait(cancellationToken);
                var context = await task.ConfigureAwait(false);
#endif

                Interlocked.Increment(ref this.activeRequestCount);
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await this.ProcessRequestAsync(context, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref this.activeRequestCount);
                        }
                    },
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException ex)
        {
            PrometheusExporterEventSource.Log.CanceledExport(ex);
        }
        finally
        {
            // If the worker exited due to an external token cancellation (not
            // Dispose), clean up the listener here. When Dispose() is the caller
            // it will handle stop/close itself after draining in-flight requests.
            if (!this.disposed)
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
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (this.disposed || cancellationToken.IsCancellationRequested)
        {
            context.Response.StatusCode = 503;

            try
            {
                context.Response.Close();
            }
            catch
            {
            }

            return;
        }

        try
        {
            var openMetricsRequested = AcceptsOpenMetrics(context.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested).ConfigureAwait(false);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                    await context.Response.OutputStream.WriteAsync(dataView.Array.AsMemory(0, dataView.Count), cancellationToken).ConfigureAwait(false);
#else
                    await context.Response.OutputStream.WriteAsync(dataView.Array, 0, dataView.Count, cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            context.Response.StatusCode = 503;
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
