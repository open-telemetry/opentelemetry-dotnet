// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

// Keeps a client alive until every send that selected it has finished.
internal sealed class ReloadableExportClient : IExportClient, IDisposable
{
    private readonly Lock stateLock = new();
    private readonly Lock reloadGate = new();
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<OtlpExporterOptions> optionsMonitor;
    private readonly IOptionsMonitor<OtlpExporterBuilderOptions>? builderOptionsMonitor;
    private readonly IOptionsMonitorCache<OtlpExporterBuilderOptions>? builderOptionsCache;
    private readonly string optionsName;
    private readonly string httpClientName;
    private readonly OtlpSignalType signalType;
    private readonly OtlpExportProtocol protocol;
    private readonly Action<OtlpExporterOptions>? configureOnReload;
    private readonly IDisposable? optionsSubscription;
    private readonly IDisposable? builderOptionsSubscription;
    private ClientState current;
    private double timeoutMilliseconds;
    private bool stopped;
    private bool disposed;

    private ReloadableExportClient(
        OtlpExporterOptions initialOptions,
        IExportClient initialClient,
        bool ownsInitialHttpClient,
        IServiceProvider serviceProvider,
        string optionsName,
        string httpClientName,
        OtlpSignalType signalType,
        bool useOtlpExporter,
        Action<OtlpExporterOptions>? configureOnReload = null)
    {
        this.serviceProvider = serviceProvider;
        this.optionsName = optionsName;
        this.httpClientName = httpClientName;
        this.signalType = signalType;
        this.protocol = initialOptions.Protocol;
        this.configureOnReload = configureOnReload;
        this.current = new(initialClient, ownsInitialHttpClient);
        try
        {
            this.timeoutMilliseconds = GetTimeout(initialOptions, initialClient);
            this.optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();

            if (useOtlpExporter)
            {
                this.builderOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>();
                this.builderOptionsCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<OtlpExporterBuilderOptions>>();
                this.builderOptionsSubscription = this.builderOptionsMonitor.OnChange(this.OnBuilderOptionsChanged);
            }

            this.optionsSubscription = this.optionsMonitor.OnChange(this.OnExporterOptionsChanged);
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    internal double TimeoutMilliseconds => Volatile.Read(ref this.timeoutMilliseconds);

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        ClientState selected;
        lock (this.stateLock)
        {
#if NET
            ObjectDisposedException.ThrowIf(this.disposed, this);
#else
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ReloadableExportClient));
            }
#endif

            if (this.stopped)
            {
                throw new InvalidOperationException("The export client has been shut down.");
            }

            selected = this.current;
            selected.BeginSend();
        }

        try
        {
            return selected.Client.SendExportRequest(buffer, contentLength, deadlineUtc, cancellationToken);
        }
        finally
        {
            selected.EndSend();
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        ClientState selected;
        lock (this.stateLock)
        {
            if (this.stopped)
            {
                return true;
            }

            this.stopped = true;
            selected = this.current;
        }

        this.optionsSubscription?.Dispose();
        this.builderOptionsSubscription?.Dispose();

        bool result;
        try
        {
            result = selected.Client.Shutdown(timeoutMilliseconds);
        }
        finally
        {
            selected.Retire();
        }

        return result;
    }

    public void Dispose()
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.Shutdown(Timeout.Infinite);
    }

    internal static ReloadableExportClient Create(
        OtlpExporterOptions options,
        IServiceProvider serviceProvider,
        string optionsName,
        OtlpSignalType signalType,
        bool useOtlpExporter,
        bool usesHttpClientFactory,
        Action<OtlpExporterOptions>? configureOnReload = null)
    {
        var ownsHttpClient = usesHttpClientFactory || ReferenceEquals(options.HttpClientFactory, options.DefaultHttpClientFactory);
        var httpClientName = signalType switch
        {
            OtlpSignalType.Traces => OtlpExporterHttpClientNames.TraceExporter,
            OtlpSignalType.Metrics => OtlpExporterHttpClientNames.MetricExporter,
            OtlpSignalType.Logs => OtlpExporterHttpClientNames.LogExporter,
            _ => throw new NotSupportedException(),
        };
        IExportClient initialClient = usesHttpClientFactory && signalType == OtlpSignalType.Logs
            ? new LazyExportClient(() => options.GetExportClient(signalType, ownsHttpClient))
            : options.GetExportClient(signalType, ownsHttpClient);

        return new(
            options,
            initialClient,
            ownsHttpClient,
            serviceProvider,
            optionsName,
            httpClientName,
            signalType,
            useOtlpExporter,
            configureOnReload);
    }

    private static double GetTimeout(OtlpExporterOptions options, IExportClient client) =>
        client is OtlpHttpExportClient httpClient
            ? httpClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

    private void OnBuilderOptionsChanged(OtlpExporterBuilderOptions options, string? name)
        => this.OnOptionsChanged(name, invalidateBuilderOptionsCache: false);

    private void OnExporterOptionsChanged(OtlpExporterOptions options, string? name)
        => this.OnOptionsChanged(name, invalidateBuilderOptionsCache: true);

    private void OnOptionsChanged(string? name, bool invalidateBuilderOptionsCache)
    {
        if (!string.Equals(name, this.optionsName, StringComparison.Ordinal))
        {
            return;
        }

        if (invalidateBuilderOptionsCache)
        {
            this.builderOptionsCache?.TryRemove(this.optionsName);
        }

        this.Reload();
    }

    private void Reload()
    {
        lock (this.reloadGate)
        {
            lock (this.stateLock)
            {
                if (this.stopped)
                {
                    return;
                }
            }

            try
            {
                var options = this.GetOptions();
                this.configureOnReload?.Invoke(options);
                if (options.Protocol != this.protocol)
                {
                    // The exporter serializes gRPC framing at construction time.
                    OpenTelemetryProtocolExporterEventSource.Log.ExportClientProtocolChangeIgnored();
                    return;
                }

                var ownsHttpClient = options.TryEnableIHttpClientFactoryIntegration(this.serviceProvider, this.httpClientName)
                    || ReferenceEquals(options.HttpClientFactory, options.DefaultHttpClientFactory);
                var client = options.GetExportClient(this.signalType, ownsHttpClient);
                var next = new ClientState(client, ownsHttpClient);
                var nextTimeout = GetTimeout(options, client);
                ClientState old;

                lock (this.stateLock)
                {
                    if (this.stopped)
                    {
                        next.Retire();
                        return;
                    }

                    old = this.current;
                    this.current = next;
                    Volatile.Write(ref this.timeoutMilliseconds, nextTimeout);
                }

                old.Retire();
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(ex);
            }
        }
    }

    private OtlpExporterOptions GetOptions()
    {
        if (this.builderOptionsMonitor is not { } monitor)
        {
            return this.optionsMonitor.Get(this.optionsName);
        }

        var builderOptions = monitor.Get(this.optionsName);
        var signalOptions = this.signalType switch
        {
            OtlpSignalType.Traces => builderOptions.TracingOptionsInstance,
            OtlpSignalType.Metrics => builderOptions.MetricsOptionsInstance,
            OtlpSignalType.Logs => builderOptions.LoggingOptionsInstance,
            _ => throw new NotSupportedException(),
        };

        return signalOptions.ApplyDefaults(builderOptions.DefaultOptionsInstance);
    }

    private sealed class ClientState(IExportClient client, bool ownsHttpClient)
    {
        private readonly Lock sendLock = new();
        private readonly bool ownsHttpClient = ownsHttpClient;
        private int activeSends;
        private bool retired;

        internal IExportClient Client { get; } = client;

        internal void BeginSend()
        {
            lock (this.sendLock)
            {
                this.activeSends++;
            }
        }

        internal void EndSend()
        {
            bool release;
            lock (this.sendLock)
            {
                release = --this.activeSends == 0 && this.retired;
            }

            if (release)
            {
                this.ReleaseClient();
            }
        }

        internal void Retire()
        {
            bool release;
            lock (this.sendLock)
            {
                if (this.retired)
                {
                    return;
                }

                this.retired = true;
                release = this.activeSends == 0;
            }

            if (release)
            {
                this.ReleaseClient();
            }
        }

        private void ReleaseClient()
        {
            if (this.ownsHttpClient && this.Client is OtlpExportClient exportClient)
            {
                exportClient.HttpClient.Dispose();
            }
            else if (this.ownsHttpClient && this.Client is LazyExportClient lazyClient)
            {
                lazyClient.DisposeHttpClient();
            }
        }
    }
}
