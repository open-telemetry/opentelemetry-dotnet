// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

// Observes a named registration and creates clients from its current options.
internal sealed class OtlpExportClientRegistration : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<OtlpExporterOptions> optionsMonitor;
    private readonly IOptionsMonitor<OtlpExporterBuilderOptions>? builderOptionsMonitor;
    private readonly IOptionsMonitorCache<OtlpExporterBuilderOptions>? builderOptionsCache;
    private readonly string optionsName;
    private readonly string httpClientName;
    private readonly OtlpSignalType signalType;
    private readonly OtlpExportProtocol protocol;
    private readonly Action<OtlpExporterOptions>? configureOnReload;
    private IDisposable? optionsSubscription;
    private IDisposable? builderOptionsSubscription;
    private Action? reload;

    internal OtlpExportClientRegistration(
        OtlpExporterOptions initialOptions,
        IServiceProvider serviceProvider,
        string optionsName,
        OtlpSignalType signalType,
        bool useOtlpExporter,
        Action<OtlpExporterOptions>? configureOnReload)
    {
        this.serviceProvider = serviceProvider;
        this.optionsName = optionsName;
        this.signalType = signalType;
        this.protocol = initialOptions.Protocol;
        this.configureOnReload = configureOnReload;
        this.httpClientName = signalType switch
        {
            OtlpSignalType.Traces => OtlpExporterHttpClientNames.TraceExporter,
            OtlpSignalType.Metrics => OtlpExporterHttpClientNames.MetricExporter,
            OtlpSignalType.Logs => OtlpExporterHttpClientNames.LogExporter,
            _ => throw new NotSupportedException(),
        };
        this.optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();
        if (useOtlpExporter)
        {
            this.builderOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>();
            this.builderOptionsCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<OtlpExporterBuilderOptions>>();
        }
    }

    public void Dispose()
    {
        this.optionsSubscription?.Dispose();
        this.builderOptionsSubscription?.Dispose();
    }

    internal static double GetTimeout(OtlpExporterOptions options, IExportClient client) =>
        client is OtlpHttpExportClient httpClient
            ? httpClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

    internal void Subscribe(Action reload)
    {
        this.reload = reload;
        this.builderOptionsSubscription = this.builderOptionsMonitor?.OnChange(this.OnBuilderOptionsChanged);
        this.optionsSubscription = this.optionsMonitor.OnChange(this.OnExporterOptionsChanged);
    }

    internal (IExportClient Client, bool OwnsHttpClient, double TimeoutMilliseconds)? CreateExportClient()
    {
        var options = this.GetOptions();
        this.configureOnReload?.Invoke(options);
        if (options.Protocol != this.protocol)
        {
            // The exporter serializes gRPC framing at construction time.
            OpenTelemetryProtocolExporterEventSource.Log.ExportClientProtocolChangeIgnored();
            return null;
        }

        var ownsHttpClient = options.TryEnableIHttpClientFactoryIntegration(this.serviceProvider, this.httpClientName)
            || ReferenceEquals(options.HttpClientFactory, options.DefaultHttpClientFactory);
        var client = options.GetExportClient(this.signalType, ownsHttpClient);
        return (client, ownsHttpClient, GetTimeout(options, client));
    }

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

        this.reload?.Invoke();
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
}
