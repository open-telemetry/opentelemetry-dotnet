// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class ReloadableExportClientOptionsTests
{
    [Theory]
    [InlineData(false, "reload", true)]
    [InlineData(true, "reload", true)]
    [InlineData(false, "other", false)]
    [InlineData(true, "other", false)]
    [InlineData(false, "Reload", false)]
    [InlineData(true, "Reload", false)]
    [InlineData(false, null, false)]
    [InlineData(true, null, false)]
    public void OptionsCallbacksFilterNamesAndPreserveCacheInvalidation(bool builderChange, string? changedName, bool shouldReload)
    {
        using var httpClient = new HttpClient();
        var cache = new TrackingOptionsCache<OtlpExporterBuilderOptions>();
        var factoryCalls = 0;
        var invalidationsAtLastCreation = 0;
        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () =>
            {
                factoryCalls++;
                invalidationsAtLastCreation = cache.RemovedNames.Count;
                return httpClient;
            },
        };
        var configuration = new ConfigurationBuilder().Build();
        var builderOptions = new OtlpExporterBuilderOptions(
            configuration,
            options,
            new SdkLimitOptions(configuration),
            new ExperimentalOptions(configuration),
            logRecordExportProcessorOptions: null,
            metricReaderOptions: null,
            new ActivityExportProcessorOptions());
        builderOptions.TracingOptionsInstance.Protocol = OtlpExportProtocol.HttpProtobuf;
        builderOptions.TracingOptionsInstance.HttpClientFactory = options.HttpClientFactory;
        var optionsMonitor = new TestOptionsMonitor<OtlpExporterOptions>(options);
        var builderMonitor = new TestOptionsMonitor<OtlpExporterBuilderOptions>(builderOptions);
        var services = new ServiceCollection();
        services.AddSingleton<IOptionsMonitor<OtlpExporterOptions>>(optionsMonitor);
        services.AddSingleton<IOptionsMonitor<OtlpExporterBuilderOptions>>(builderMonitor);
        services.AddSingleton<IOptionsMonitorCache<OtlpExporterBuilderOptions>>(cache);
        using var serviceProvider = services.BuildServiceProvider();
        using var client = ReloadableExportClient.Create(
            options,
            serviceProvider,
            "reload",
            OtlpSignalType.Traces,
            useOtlpExporter: true,
            usesHttpClientFactory: false);
        Assert.Equal(1, factoryCalls);

        if (builderChange)
        {
            builderMonitor.Notify(changedName);
        }
        else
        {
            optionsMonitor.Notify(changedName);
        }

        var expectedInvalidations = shouldReload && !builderChange ? 1 : 0;
        Assert.Equal(shouldReload ? 2 : 1, factoryCalls);
        Assert.Equal(expectedInvalidations, cache.RemovedNames.Count);
        Assert.Equal(expectedInvalidations, invalidationsAtLastCreation);
        if (expectedInvalidations == 1)
        {
            Assert.Equal("reload", Assert.Single(cache.RemovedNames));
        }
    }

    private sealed class TestOptionsMonitor<T>(T options) : IOptionsMonitor<T>
        where T : class
    {
        private Action<T, string?>? listener;

        public T CurrentValue => options;

        public T Get(string? name) => options;

        public IDisposable OnChange(Action<T, string?> listener)
        {
            this.listener = listener;
            return new Subscription(this);
        }

        internal void Notify(string? name) => this.listener?.Invoke(options, name);

        private sealed class Subscription(TestOptionsMonitor<T> monitor) : IDisposable
        {
            public void Dispose() => monitor.listener = null;
        }
    }

    private sealed class TrackingOptionsCache<T> : IOptionsMonitorCache<T>
        where T : class
    {
        private readonly OptionsCache<T> cache = new();

        internal List<string?> RemovedNames { get; } = [];

        public T GetOrAdd(string? name, Func<T> createOptions) => this.cache.GetOrAdd(name, createOptions);

        public bool TryAdd(string? name, T options) => this.cache.TryAdd(name, options);

        public bool TryRemove(string? name)
        {
            this.RemovedNames.Add(name);
            return this.cache.TryRemove(name);
        }

        public void Clear() => this.cache.Clear();
    }
}
