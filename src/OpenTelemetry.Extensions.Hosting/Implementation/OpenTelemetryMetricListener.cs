// <copyright file="OpenTelemetryMetricListener.cs" company="OpenTelemetry Authors">
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

internal sealed class OpenTelemetryMetricListener : IMetricsListener
{
    private readonly MeterProviderSdk meterProviderSdk;
    private IObservableInstrumentsSource? observableInstrumentsSource;

    public OpenTelemetryMetricListener(MeterProvider meterProvider)
    {
        var meterProviderSdk = meterProvider as MeterProviderSdk;

        Debug.Assert(meterProviderSdk != null, "meterProvider was not MeterProviderSdk");

        this.meterProviderSdk = meterProviderSdk!;

        this.meterProviderSdk.OnCollectObservableInstruments += () =>
        {
            this.observableInstrumentsSource?.RecordObservableInstruments();
        };
    }

    public string Name => "OpenTelemetry";

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
        userState = this.meterProviderSdk.InstrumentPublished(instrument, skipShouldListenToCheck: true);
        return userState != null;
    }

    public void MeasurementsCompleted(Instrument instrument, object? userState)
    {
        this.meterProviderSdk.MeasurementsCompleted(instrument, userState);
    }

    public void Initialize(IObservableInstrumentsSource source)
    {
        this.observableInstrumentsSource = source;
    }

    private void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? state)
    {
        var meterProvider = this.meterProviderSdk;

        // todo: Refactor SDK so we don't double-check the state

        if (state is List<Metric> || state is List<List<Metric>>)
        {
            meterProvider.MeasurementRecordedDouble(instrument, value, tagsRos, state);
        }
        else if (state is Metric)
        {
            meterProvider.MeasurementRecordedDoubleSingleStream(instrument, value, tagsRos, state);
        }
        else
        {
            // todo: Log dropped metric
        }
    }

    private void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? state)
    {
        var meterProvider = this.meterProviderSdk;

        // todo: Refactor SDK so we don't double-check the state

        if (state is List<Metric> || state is List<List<Metric>>)
        {
            meterProvider.MeasurementRecordedLong(instrument, value, tagsRos, state);
        }
        else if (state is Metric)
        {
            meterProvider.MeasurementRecordedLongSingleStream(instrument, value, tagsRos, state);
        }
        else
        {
            // todo: Log dropped metric
        }
    }
}
