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
    private IMetricsSource? metricsSource;

    public OpenTelemetryMetricListener(MeterProvider meterProvider)
    {
        var meterProviderSdk = meterProvider as MeterProviderSdk;

        Debug.Assert(meterProviderSdk != null, "meterProvider was not MeterProviderSdk");

        this.meterProviderSdk = meterProviderSdk!;

        this.meterProviderSdk.OnCollectObservableInstruments += () =>
        {
            this.metricsSource?.RecordObservableInstruments();
        };
    }

    public string Name => "OpenTelemetry";

    public MeasurementCallback<T> GetMeasurementHandler<T>()
        where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, (byte)(object)value, tags, state);
        }
        else if (typeof(T) == typeof(short))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, (short)(object)value, tags, state);
        }
        else if (typeof(T) == typeof(int))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, (int)(object)value, tags, state);
        }
        else if (typeof(T) == typeof(long))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedLong(instrument, (long)(object)value, tags, state);
        }
        else if (typeof(T) == typeof(float))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedDouble(instrument, (float)(object)value, tags, state);
        }
        else if (typeof(T) == typeof(double))
        {
            return (instrument, value, tags, state)
                => this.MeasurementRecordedDouble(instrument, (double)(object)value, tags, state);
        }
        else
        {
            return MeasurementRecordedUnknown;
        }
    }

    public object? InstrumentPublished(Instrument instrument)
    {
        return this.meterProviderSdk.InstrumentPublished(instrument, skipShouldListenToCheck: true);
    }

    public void MeasurementsCompleted(Instrument instrument, object? userState)
    {
        this.meterProviderSdk.MeasurementsCompleted(instrument, userState);
    }

    public void SetSource(IMetricsSource source)
    {
        this.metricsSource = source;
    }

    private static void MeasurementRecordedUnknown<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? state)
    {
        // todo: Log dropped metric
    }

    private void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tagsRos, object? state)
    {
        var meterProvider = this.meterProviderSdk;

        if (state is List<Metric>)
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

        if (state is List<Metric>)
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
