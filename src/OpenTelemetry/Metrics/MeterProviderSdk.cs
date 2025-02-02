// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

internal sealed class MeterProviderSdk : MeterProvider
{
    internal const string ExemplarFilterConfigKey = "OTEL_METRICS_EXEMPLAR_FILTER";
    internal const string ExemplarFilterHistogramsConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS";

    internal readonly IServiceProvider ServiceProvider;
    internal readonly IDisposable? OwnedServiceProvider;
    internal int ShutdownCount;
    internal bool Disposed;
    internal ExemplarFilterType? ExemplarFilter;
    internal ExemplarFilterType? ExemplarFilterForHistograms;
    internal Action? OnCollectObservableInstruments;

    private readonly List<object> instrumentations = new();
    private readonly List<Func<Instrument, MetricStreamConfiguration?>> viewConfigs;
    private readonly Lock collectLock = new();
    private readonly MeterListener listener;
    private readonly MetricReader? reader;
    private readonly CompositeMetricReader? compositeMetricReader;
    private readonly Func<Instrument, bool> shouldListenTo = instrument => false;

    internal MeterProviderSdk(
        IServiceProvider serviceProvider,
        bool ownsServiceProvider)
    {
        var state = serviceProvider.GetRequiredService<MeterProviderBuilderSdk>();
        state.RegisterProvider(this);

        this.ServiceProvider = serviceProvider;

        if (ownsServiceProvider)
        {
            this.OwnedServiceProvider = serviceProvider as IDisposable;
            Debug.Assert(this.OwnedServiceProvider != null, "serviceProvider was not IDisposable");
        }

        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("Building MeterProvider.");

        var configureProviderBuilders = serviceProvider.GetServices<IConfigureMeterProviderBuilder>();
        foreach (var configureProviderBuilder in configureProviderBuilders)
        {
            configureProviderBuilder.ConfigureBuilder(serviceProvider, state);
        }

        this.ExemplarFilter = state.ExemplarFilter;

        this.ApplySpecificationConfigurationKeys(serviceProvider.GetRequiredService<IConfiguration>());

        StringBuilder exportersAdded = new StringBuilder();
        StringBuilder instrumentationFactoriesAdded = new StringBuilder();

        var resourceBuilder = state.ResourceBuilder ?? ResourceBuilder.CreateDefault();
        resourceBuilder.ServiceProvider = serviceProvider;
        this.Resource = resourceBuilder.Build();

        this.viewConfigs = state.ViewConfigs;

        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
            $"MeterProvider configuration: {{MetricLimit={state.MetricLimit}, CardinalityLimit={state.CardinalityLimit}, ExemplarFilter={this.ExemplarFilter}, ExemplarFilterForHistograms={this.ExemplarFilterForHistograms}}}.");

        foreach (var reader in state.Readers)
        {
            Guard.ThrowIfNull(reader);

            reader.SetParentProvider(this);

            reader.ApplyParentProviderSettings(
                state.MetricLimit,
                state.CardinalityLimit,
                this.ExemplarFilter,
                this.ExemplarFilterForHistograms);

            if (this.reader == null)
            {
                this.reader = reader;
            }
            else if (this.reader is CompositeMetricReader compositeReader)
            {
                compositeReader.AddReader(reader);
            }
            else
            {
                this.reader = new CompositeMetricReader(new[] { this.reader, reader });
            }

            if (reader is PeriodicExportingMetricReader periodicExportingMetricReader)
            {
                exportersAdded.Append(periodicExportingMetricReader.Exporter);
                exportersAdded.Append(" (Paired with PeriodicExportingMetricReader exporting at ");
                exportersAdded.Append(periodicExportingMetricReader.ExportIntervalMilliseconds);
                exportersAdded.Append(" milliseconds intervals.)");
                exportersAdded.Append(';');
            }
            else if (reader is BaseExportingMetricReader baseExportingMetricReader)
            {
                exportersAdded.Append(baseExportingMetricReader.Exporter);
                exportersAdded.Append(" (Paired with a MetricReader requiring manual trigger to export.)");
                exportersAdded.Append(';');
            }
        }

        if (exportersAdded.Length != 0)
        {
            exportersAdded.Remove(exportersAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exporters added = \"{exportersAdded}\".");
        }

        this.compositeMetricReader = this.reader as CompositeMetricReader;

        if (state.Instrumentation.Any())
        {
            foreach (var instrumentation in state.Instrumentation)
            {
                if (instrumentation.Instance is not null)
                {
                    this.instrumentations.Add(instrumentation.Instance);
                }

                instrumentationFactoriesAdded.Append(instrumentation.Name);
                instrumentationFactoriesAdded.Append(';');
            }
        }

        if (instrumentationFactoriesAdded.Length != 0)
        {
            instrumentationFactoriesAdded.Remove(instrumentationFactoriesAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Instrumentations added = \"{instrumentationFactoriesAdded}\".");
        }

        // Setup Listener
        if (state.MeterSources.Any(s => WildcardHelper.ContainsWildcard(s)))
        {
            var regex = WildcardHelper.GetWildcardRegex(state.MeterSources);
            this.shouldListenTo = instrument => regex.IsMatch(instrument.Meter.Name);
        }
        else if (state.MeterSources.Any())
        {
            var meterSourcesToSubscribe = new HashSet<string>(state.MeterSources, StringComparer.OrdinalIgnoreCase);
            this.shouldListenTo = instrument => meterSourcesToSubscribe.Contains(instrument.Meter.Name);
        }

        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Listening to following meters = \"{string.Join(";", state.MeterSources)}\".");

        this.listener = new MeterListener();
        var viewConfigCount = this.viewConfigs.Count;

        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Number of views configured = {viewConfigCount}.");

        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            object? state = this.InstrumentPublished(instrument, listeningIsManagedExternally: false);
            if (state != null)
            {
                listener.EnableMeasurementEvents(instrument, state);
            }
        };

        // Everything double
        this.listener.SetMeasurementEventCallback<double>(MeasurementRecordedDouble);
        this.listener.SetMeasurementEventCallback<float>(static (instrument, value, tags, state) => MeasurementRecordedDouble(instrument, value, tags, state));

        // Everything long
        this.listener.SetMeasurementEventCallback<long>(MeasurementRecordedLong);
        this.listener.SetMeasurementEventCallback<int>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));
        this.listener.SetMeasurementEventCallback<short>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));
        this.listener.SetMeasurementEventCallback<byte>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));

        this.listener.MeasurementsCompleted = MeasurementsCompleted;

        this.listener.Start();

        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("MeterProvider built successfully.");
    }

    internal Resource Resource { get; }

    internal List<object> Instrumentations => this.instrumentations;

    internal MetricReader? Reader => this.reader;

    internal int ViewCount => this.viewConfigs.Count;

    internal static void MeasurementsCompleted(Instrument instrument, object? state)
    {
        if (state is not MetricState metricState)
        {
            // todo: Log
            return;
        }

        metricState.CompleteMeasurement();
    }

    internal static void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (state is not MetricState metricState)
        {
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument?.Name ?? "UnknownInstrument", "SDK internal error occurred.", "Contact SDK owners.");
            return;
        }

        metricState.RecordMeasurementLong(value, tags);
    }

    internal static void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (state is not MetricState metricState)
        {
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument?.Name ?? "UnknownInstrument", "SDK internal error occurred.", "Contact SDK owners.");
            return;
        }

        metricState.RecordMeasurementDouble(value, tags);
    }

    internal object? InstrumentPublished(Instrument instrument, bool listeningIsManagedExternally)
    {
        var listenToInstrumentUsingSdkConfiguration = this.shouldListenTo(instrument);

        if (listeningIsManagedExternally && listenToInstrumentUsingSdkConfiguration)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                instrument.Name,
                instrument.Meter.Name,
                "Instrument belongs to a Meter which has been enabled both externally and via a subscription on the provider. External subscription will be ignored in favor of the provider subscription.",
                "Programmatic calls adding meters to the SDK (either by calling AddMeter directly or indirectly through helpers such as 'AddInstrumentation' extensions) are always favored over external registrations. When also using external management (typically IMetricsBuilder or IMetricsListener) remove programmatic calls to the SDK to allow registrations to be added and removed dynamically.");
            return null;
        }
        else if (!listenToInstrumentUsingSdkConfiguration && !listeningIsManagedExternally)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                instrument.Name,
                instrument.Meter.Name,
                "Instrument belongs to a Meter not subscribed by the provider.",
                "Use AddMeter to add the Meter to the provider.");
            return null;
        }

        object? state = null;
        var viewConfigCount = this.viewConfigs.Count;

        try
        {
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Started publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");

            if (viewConfigCount <= 0)
            {
                if (!MeterProviderBuilderSdk.IsValidInstrumentName(instrument.Name))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                        instrument.Name,
                        instrument.Meter.Name,
                        "Instrument name is invalid.",
                        "The name must comply with the OpenTelemetry specification");
                    return null;
                }

                if (this.reader != null)
                {
                    var metrics = this.reader.AddMetricWithNoViews(instrument);
                    if (metrics.Count == 1)
                    {
                        state = MetricState.BuildForSingleMetric(metrics[0]);
                    }
                    else if (metrics.Count > 0)
                    {
                        state = MetricState.BuildForMetricList(metrics);
                    }
                }
            }
            else
            {
                // Creating list with initial capacity as the maximum
                // possible size, to avoid any array resize/copy internally.
                // There may be excess space wasted, but it'll eligible for
                // GC right after this method.
                var metricStreamConfigs = new List<MetricStreamConfiguration?>(viewConfigCount);
                for (var i = 0; i < viewConfigCount; ++i)
                {
                    var viewConfig = this.viewConfigs[i];
                    MetricStreamConfiguration? metricStreamConfig = null;

                    try
                    {
                        metricStreamConfig = viewConfig(instrument);

                        // The SDK provides some static MetricStreamConfigurations.
                        // For example, the Drop configuration. The static ViewId
                        // should not be changed for these configurations.
                        if (metricStreamConfig != null && !metricStreamConfig.ViewId.HasValue)
                        {
                            metricStreamConfig.ViewId = i;
                        }

                        if (metricStreamConfig is HistogramConfiguration
                            && instrument.GetType().GetGenericTypeDefinition() != typeof(Histogram<>))
                        {
                            metricStreamConfig = null;

                            OpenTelemetrySdkEventSource.Log.MetricViewIgnored(
                                instrument.Name,
                                instrument.Meter.Name,
                                "The current SDK does not allow aggregating non-Histogram instruments as Histograms.",
                                "Fix the view configuration.");
                        }
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricViewIgnored(instrument.Name, instrument.Meter.Name, ex.Message, "Fix the view configuration.");
                    }

                    if (metricStreamConfig != null)
                    {
                        metricStreamConfigs.Add(metricStreamConfig);
                    }
                }

                if (metricStreamConfigs.Count == 0)
                {
                    // No views matched. Add null
                    // which will apply defaults.
                    // Users can turn off this default
                    // by adding a view like below as the last view.
                    // .AddView(instrumentName: "*", MetricStreamConfiguration.Drop)
                    metricStreamConfigs.Add(null);
                }

                if (this.reader != null)
                {
                    var metrics = this.reader.AddMetricWithViews(instrument, metricStreamConfigs);
                    if (metrics.Count == 1)
                    {
                        state = MetricState.BuildForSingleMetric(metrics[0]);
                    }
                    else if (metrics.Count > 0)
                    {
                        state = MetricState.BuildForMetricList(metrics);
                    }
                }
            }

            if (state != null)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Measurements for Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\" will be processed and aggregated by the SDK.");
                return state;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Measurements for Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\" will be dropped by the SDK.");
                return null;
            }
        }
#if DEBUG
        catch (Exception ex)
        {
            throw new InvalidOperationException("SDK internal error occurred.", ex);
        }
#else
        catch (Exception)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "SDK internal error occurred.", "Contact SDK owners.");
            return null;
        }
#endif
    }

    internal void CollectObservableInstruments()
    {
        lock (this.collectLock)
        {
            // Record all observable instruments
            try
            {
                this.listener.RecordObservableInstruments();

                this.OnCollectObservableInstruments?.Invoke();
            }
            catch (Exception exception)
            {
                // TODO:
                // It doesn't looks like we can find which instrument callback
                // threw.
                OpenTelemetrySdkEventSource.Log.MetricObserverCallbackException(exception);
            }
        }
    }

    /// <summary>
    /// Called by <c>ForceFlush</c>. This function should block the current
    /// thread until flush completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the thread which made the
    /// first call to <c>ForceFlush</c>. This function should not throw
    /// exceptions.
    /// </remarks>
    internal bool OnForceFlush(int timeoutMilliseconds)
    {
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnForceFlush)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");
        return this.reader?.Collect(timeoutMilliseconds) ?? true;
    }

    /// <summary>
    /// Called by <c>Shutdown</c>. This function should block the current
    /// thread until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the thread which made the
    /// first call to <c>Shutdown</c>. This function should not throw
    /// exceptions.
    /// </remarks>
    internal bool OnShutdown(int timeoutMilliseconds)
    {
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnShutdown)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");
        return this.reader?.Shutdown(timeoutMilliseconds) ?? true;
    }

    protected override void Dispose(bool disposing)
    {
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.Dispose)} started.");
        if (!this.Disposed)
        {
            if (disposing)
            {
                if (this.instrumentations != null)
                {
                    foreach (var item in this.instrumentations)
                    {
                        (item as IDisposable)?.Dispose();
                    }

                    this.instrumentations.Clear();
                }

                // Wait for up to 5 seconds grace period
                this.reader?.Shutdown(5000);
                this.reader?.Dispose();
                this.compositeMetricReader?.Dispose();

                this.listener?.Dispose();

                this.OwnedServiceProvider?.Dispose();
            }

            this.Disposed = true;
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(MeterProvider));
        }

        base.Dispose(disposing);
    }

    private void ApplySpecificationConfigurationKeys(IConfiguration configuration)
    {
        var hasProgrammaticExemplarFilterValue = this.ExemplarFilter.HasValue;

        if (configuration.TryGetStringValue(ExemplarFilterConfigKey, out var configValue))
        {
            if (hasProgrammaticExemplarFilterValue)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
                    $"Exemplar filter configuration value '{configValue}' has been ignored because a value '{this.ExemplarFilter}' was set programmatically.");
                return;
            }

            if (!TryParseExemplarFilterFromConfigurationValue(configValue, out var exemplarFilter))
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exemplar filter configuration was found but the value '{configValue}' is invalid and will be ignored.");
                return;
            }

            this.ExemplarFilter = exemplarFilter;

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exemplar filter set to '{exemplarFilter}' from configuration.");
        }

        if (configuration.TryGetStringValue(ExemplarFilterHistogramsConfigKey, out configValue))
        {
            if (hasProgrammaticExemplarFilterValue)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
                    $"Exemplar filter histogram configuration value '{configValue}' has been ignored because a value '{this.ExemplarFilter}' was set programmatically.");
                return;
            }

            if (!TryParseExemplarFilterFromConfigurationValue(configValue, out var exemplarFilter))
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exemplar filter histogram configuration was found but the value '{configValue}' is invalid and will be ignored.");
                return;
            }

            this.ExemplarFilterForHistograms = exemplarFilter;

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exemplar filter for histograms set to '{exemplarFilter}' from configuration.");
        }

        static bool TryParseExemplarFilterFromConfigurationValue(string? configValue, out ExemplarFilterType? exemplarFilter)
        {
            if (string.Equals("always_off", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.AlwaysOff;
                return true;
            }

            if (string.Equals("always_on", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.AlwaysOn;
                return true;
            }

            if (string.Equals("trace_based", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.TraceBased;
                return true;
            }

            exemplarFilter = null;
            return false;
        }
    }
}
