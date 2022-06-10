using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Shared;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.HTTP.Server
{
    /// <summary>
    /// An HTTP listener used to expose Prometheus metrics.
    /// </summary>
    [ExportModes(ExportModes.Pull)]
    public sealed class PrometheusExporterHttpServer : BaseExporter<Metric>, IPullMetricExporter, IDisposable
    {
        internal const string HttpListenerStartFailureExceptionMessage = "PrometheusExporter http listener could not be started.";
        private readonly HttpListener listener = new();
        private readonly object syncObject = new();

        private Func<int, bool> funcCollect;
        private Func<Batch<Metric>, ExportResult> funcExport;

        private CancellationTokenSource tokenSource;
        private Task workerThread;

        private bool disposed;

        public PrometheusExporterHttpServer(PrometheusExporterHttpServerOptions options)
        {
            if (options.StartHttpListener)
            {
                try
                {
                    if ((options.HttpListenerPrefixes?.Count ?? 0) <= 0)
                    {
                        throw new ArgumentException("No HttpListenerPrefixes were specified on PrometheusExporterHttpServerOptions.");
                    }

                    string path = options.ScrapeEndpointPath ?? PrometheusExporterHttpServerOptions.DefaultScrapeEndpointPath;
                    if (!path.StartsWith("/"))
                    {
                        path = $"/{path}";
                    }

                    if (!path.EndsWith("/"))
                    {
                        path = $"{path}/";
                    }

                    foreach (string prefix in options.HttpListenerPrefixes)
                    {
                        this.listener.Prefixes.Add($"{prefix.TrimEnd('/')}{path}");
                    }

                    this.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(HttpListenerStartFailureExceptionMessage, ex);
                }
            }

            this.CollectionManager = new PrometheusCollectionManager(this);
        }

        public Func<int, bool> Collect
        {
            get => this.funcCollect;
            set => this.funcCollect = value;
        }

        public Func<Batch<Metric>, ExportResult> OnExport
        {
            get => this.funcExport;
            set => this.funcExport = value;
        }

        internal PrometheusCollectionManager CollectionManager { get; }

        /// <summary>
        /// Start Http Server.
        /// </summary>
        /// <param name="token">An optional <see cref="CancellationToken"/> that can be used to stop the HTTP server.</param>
        public void Start(CancellationToken token = default)
        {
            lock (this.syncObject)
            {
                if (this.tokenSource != null)
                {
                    return;
                }
            }

            // link the passed in token if not null
            this.tokenSource = token == default ?
                new CancellationTokenSource() :
                CancellationTokenSource.CreateLinkedTokenSource(token);

            this.workerThread = Task.Factory.StartNew(this.WorkerProc, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

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

        public override ExportResult Export(in Batch<Metric> metrics)
        {
            return this.OnExport(metrics);
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this?.Dispose();
                    if (this.listener != null && this.listener.IsListening)
                    {
                        this.Stop();
                        this.listener.Close();
                    }
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void WorkerProc()
        {
            this.listener.Start();

            try
            {
                using var scope = SuppressInstrumentationScope.Begin();
                while (!this.tokenSource.IsCancellationRequested)
                {
                    var ctxTask = this.listener.GetContextAsync();
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
                    this.listener.Stop();
                    this.listener.Close();
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
                var collectionResponse = await this.CollectionManager.EnterCollect().ConfigureAwait(false);
                try
                {
                    context.Response.Headers.Add("Server", string.Empty);
                    if (collectionResponse.View.Count > 0)
                    {
                        context.Response.StatusCode = 200;
                        context.Response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
                        context.Response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

                        await context.Response.OutputStream.WriteAsync(collectionResponse.View.Array, 0, collectionResponse.View.Count).ConfigureAwait(false);
                    }
                    else
                    {
                        // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
                        context.Response.StatusCode = 204;
                        PrometheusExporterEventSource.Log.NoMetrics();
                    }
                }
                finally
                {
                    this.CollectionManager.ExitCollect();
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
}
