// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

internal sealed class OpenTelemetryMetricsListener : IMetricsListener, IDisposable
{
    private readonly MeterProviderSdk meterProviderSdk;
    private IObservableInstrumentsSource? observableInstrumentsSource;

    public OpenTelemetryMetricsListener(MeterProvider meterProvider)
    {
        var meterProviderSdk = meterProvider as MeterProviderSdk;

        Debug.Assert(meterProviderSdk != null, "meterProvider was not MeterProviderSdk");

        this.meterProviderSdk = meterProviderSdk!;

        this.meterProviderSdk.OnCollectObservableInstruments += this.OnCollectObservableInstruments;
    }

    public string Name => "OpenTelemetry";

    public void Dispose()
    {
        this.meterProviderSdk.OnCollectObservableInstruments -= this.OnCollectObservableInstruments;
    }

    public MeasurementHandlers GetMeasurementHandlers()
    {
        return new MeasurementHandlers()
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
    }

    public bool InstrumentPublished(Instrument instrument, out object? userState)
    {
        userState = this.meterProviderSdk.InstrumentPublished(instrument, listeningIsManagedExternally: true);
        return userState != null;
    }

    public void MeasurementsCompleted(Instrument instrument, object? userState)
    {
        MeterProviderSdk.MeasurementsCompleted(instrument, userState);
    }

    public void Initialize(IObservableInstrumentsSource source)
    {
        this.observableInstrumentsSource = source;
    }

    private void OnCollectObservableInstruments()
    {
        this.observableInstrumentsSource?.RecordObservableInstruments();
    }
}
