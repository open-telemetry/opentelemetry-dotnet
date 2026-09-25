// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

// Keeps a client alive until every send that selected it has finished.
internal sealed class ReloadableExportClient : IExportClient, IDisposable
{
    private readonly object gate = new();
    private readonly object reloadGate = new();
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
        this.timeoutMilliseconds = GetTimeout(initialOptions, initialClient);
        this.optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();

        if (useOtlpExporter)
        {
            this.builderOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>();
            this.builderOptionsCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<OtlpExporterBuilderOptions>>();
            this.builderOptionsSubscription = this.builderOptionsMonitor.OnChange((_, name) =>
            {
                if (string.Equals(name, this.optionsName, StringComparison.Ordinal))
                {
                    this.Reload();
                }
            });
        }

        this.optionsSubscription = this.optionsMonitor.OnChange((_, name) =>
        {
            if (string.Equals(name, this.optionsName, StringComparison.Ordinal))
            {
                this.builderOptionsCache?.TryRemove(this.optionsName);
                this.Reload();
            }
        });
    }

    internal double TimeoutMilliseconds => Volatile.Read(ref this.timeoutMilliseconds);

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        ClientState selected;
        lock (this.gate)
        {
#if NET
            ObjectDisposedException.ThrowIf(this.stopped, this);
#else
            if (this.stopped)
            {
                throw new ObjectDisposedException(nameof(ReloadableExportClient));
            }
#endif

            selected = this.current;
            selected.ActiveSends++;
        }

        try
        {
            return selected.Client.SendExportRequest(buffer, contentLength, deadlineUtc, cancellationToken);
        }
        finally
        {
            bool release;
            lock (this.gate)
            {
                release = --selected.ActiveSends == 0 && selected.Retired;
            }

            if (release)
            {
                ReleaseClient(selected);
            }
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        ClientState selected;
        lock (this.gate)
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
            bool release;
            lock (this.gate)
            {
                selected.Retired = true;
                release = selected.ActiveSends == 0;
            }

            if (release)
            {
                ReleaseClient(selected);
            }
        }

        return result;
    }

    public void Dispose() => this.Shutdown(Timeout.Infinite);

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
            OtlpSignalType.Traces => "OtlpTraceExporter",
            OtlpSignalType.Metrics => "OtlpMetricExporter",
            OtlpSignalType.Logs => "OtlpLogExporter",
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

    private static void ReleaseClient(ClientState state)
    {
        if (state.OwnsHttpClient && state.Client is OtlpExportClient client)
        {
            client.HttpClient.Dispose();
        }
        else if (state.OwnsHttpClient && state.Client is LazyExportClient lazyClient)
        {
            lazyClient.DisposeHttpClient();
        }
    }

    private void Reload()
    {
        lock (this.reloadGate)
        {
            lock (this.gate)
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
                bool releaseOld;

                lock (this.gate)
                {
                    if (this.stopped)
                    {
                        ReleaseClient(next);
                        return;
                    }

                    old = this.current;
                    this.current = next;
                    Volatile.Write(ref this.timeoutMilliseconds, nextTimeout);
                    old.Retired = true;
                    releaseOld = old.ActiveSends == 0;
                }

                if (releaseOld)
                {
                    ReleaseClient(old);
                }
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
        internal readonly IExportClient Client = client;
        internal readonly bool OwnsHttpClient = ownsHttpClient;
        internal int ActiveSends;
        internal bool Retired;
    }
}
