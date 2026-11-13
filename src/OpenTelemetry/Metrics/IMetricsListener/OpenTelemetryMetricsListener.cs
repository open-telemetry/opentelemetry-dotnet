// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

internal sealed class OpenTelemetryMetricsListener : IMetricsListener, IDisposable
{
    private MeterProviderSdk? meterProviderSdk;
    private IObservableInstrumentsSource? observableInstrumentsSource;

    public OpenTelemetryMetricsListener(MeterProvider meterProvider)
    {
        if (meterProvider is MeterProviderSdk meterProviderSdk)
        {
            this.meterProviderSdk = meterProviderSdk;
            this.meterProviderSdk.OnCollectObservableInstruments += this.OnCollectObservableInstruments;
        }
    }

    public string Name => "OpenTelemetry";

    public void Dispose()
    {
        // References are released (not just the event unsubscribed) because
        // versions of Microsoft.Extensions.Diagnostics prior to 9.0 never dispose
        // the underlying MeterListener when the host is disposed. In that case the
        // runtime keeps this listener rooted for the lifetime of the process.
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/6447.
        var provider = Interlocked.Exchange(ref this.meterProviderSdk, null);

        if (provider is { })
        {
            provider.OnCollectObservableInstruments -= this.OnCollectObservableInstruments;
        }

        this.observableInstrumentsSource = null;
    }

    public MeasurementHandlers GetMeasurementHandlers() => new()
    {
        ByteHandler = (instrument, value, tags, state)
            => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
        ShortHandler = (instrument, value, tags, state)
            => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
        IntHandler = (instrument, value, tags, state)
            => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
        LongHandler = MeterProviderSdk.MeasurementRecordedLong,
        FloatHandler = (instrument, value, tags, state)
            => MeterProviderSdk.MeasurementRecordedDouble(instrument, value, tags, state),
        DoubleHandler = MeterProviderSdk.MeasurementRecordedDouble,
    };

    public bool InstrumentPublished(Instrument instrument, out object? userState)
    {
        userState = null;
        var result = false;

        if (this.meterProviderSdk is { } provider)
        {
            userState = provider.InstrumentPublished(instrument, listeningIsManagedExternally: true);
            result = userState is not null;
        }

        return result;
    }

    public void MeasurementsCompleted(Instrument instrument, object? userState)
        => MeterProviderSdk.MeasurementsCompleted(instrument, userState);

    public void Initialize(IObservableInstrumentsSource source)
        => this.observableInstrumentsSource = source;

    private void OnCollectObservableInstruments()
        => this.observableInstrumentsSource?.RecordObservableInstruments();
}
