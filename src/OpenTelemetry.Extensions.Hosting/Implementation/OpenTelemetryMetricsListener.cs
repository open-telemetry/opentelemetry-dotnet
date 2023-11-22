// <copyright file="OpenTelemetryMetricsListener.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
                => this.MeasurementRecordedLong(instrument, value, tags, state),
            ShortHandler = (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, value, tags, state),
            IntHandler = (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, value, tags, state),
            LongHandler = this.MeasurementRecordedLong,
            FloatHandler = (instrument, value, tags, state)
                => this.MeasurementRecordedDouble(instrument, value, tags, state),
            DoubleHandler = this.MeasurementRecordedDouble,
        };
    }

    public bool InstrumentPublished(Instrument instrument, out object? userState)
    {
        userState = this.meterProviderSdk.InstrumentPublished(instrument, listeningIsManagedExternally: true);
        return userState != null;
    }

    public void MeasurementsCompleted(Instrument instrument, object? userState)
    {
        var meterProvider = this.meterProviderSdk;

        if (meterProvider.ViewCount > 0)
        {
            meterProvider.MeasurementsCompleted(instrument, userState);
        }
        else
        {
            meterProvider.MeasurementsCompletedSingleStream(instrument, userState);
        }
    }

    public void Initialize(IObservableInstrumentsSource source)
    {
        this.observableInstrumentsSource = source;
    }

    private void OnCollectObservableInstruments()
    {
        this.observableInstrumentsSource?.RecordObservableInstruments();
    }

    private void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? userState)
    {
        var meterProvider = this.meterProviderSdk;

        if (meterProvider.ViewCount > 0)
        {
            meterProvider.MeasurementRecordedDouble(instrument, value, tagsRos, userState);
        }
        else
        {
            meterProvider.MeasurementRecordedDoubleSingleStream(instrument, value, tagsRos, userState);
        }
    }

    private void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? userState)
    {
        var meterProvider = this.meterProviderSdk;

        if (meterProvider.ViewCount > 0)
        {
            meterProvider.MeasurementRecordedLong(instrument, value, tagsRos, userState);
        }
        else
        {
            meterProvider.MeasurementRecordedLongSingleStream(instrument, value, tagsRos, userState);
        }
    }
}
