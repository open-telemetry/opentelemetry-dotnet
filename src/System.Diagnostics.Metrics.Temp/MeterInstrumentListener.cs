using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public delegate void MeasurementCallback<T>(MeterInstrument instrument, T measurement, ReadOnlySpan<(string LabelName, object LabelValue)> labels, object? cookie);

    public class MeterInstrumentListener : IDisposable
    {
        Dictionary<MeterInstrument, object?> _subscribedObservableMeters = new Dictionary<MeterInstrument, object?>();
        int _countSubscribedInstruments;
        MeasurementCallback<object?> _recordObjectFunc;
        MeasurementCallback<double> _recordDoubleFunc;
        MeasurementCallback<float> _recordFloatFunc;
        MeasurementCallback<long> _recordLongFunc;
        MeasurementCallback<int> _recordIntFunc;
        MeasurementCallback<short> _recordShortFunc;
        MeasurementCallback<byte> _recordByteFunc;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
                               // SetMeasurementEventCallback will set these values
        public MeterInstrumentListener()
#pragma warning restore CS8618
        {
            SetMeasurementEventCallback<object>(null);
            SetMeasurementEventCallback<double>(null);
            SetMeasurementEventCallback<float>(null);
            SetMeasurementEventCallback<long>(null);
            SetMeasurementEventCallback<int>(null);
            SetMeasurementEventCallback<short>(null);
            SetMeasurementEventCallback<byte>(null);
        }

        public void EnablePublishingEvents()
        {
            MeterInstrumentCollection.Instance.AddInstrumentPublishedListener(this);
        }

        public void DisablePublishingEvents()
        {
            MeterInstrumentCollection.Instance.RemoveInstrumentedPublishedListener(this);
        }


        public void EnableMeasurementEvents(MeterInstrument instrument, object? cookie = null)
        {
            bool isNewlySubscribed = false;
            object? previousCookie;
            lock (MeterInstrumentCollection.Lock)
            {
                if (!instrument.IsObservable)
                {
                    isNewlySubscribed = SubscribeInstrument(instrument, cookie, out previousCookie);
                }
                else
                {
                    isNewlySubscribed = SubscribeObservableInstrument(instrument, cookie, out previousCookie);
                }
                if (isNewlySubscribed)
                {
                    _countSubscribedInstruments++;
                    if (_countSubscribedInstruments == 1)
                    {
                        MeterInstrumentCollection.Instance.AddInstrumentDisposedListener(this);
                    }
                }
            }
            if(!isNewlySubscribed)
            {
                InstrumentMeasurementsComplete?.Invoke(instrument, previousCookie);
            }
        }

        public void DisableMeasurementEvents(MeterInstrument instrument)
        {
            object? cookie;
            bool wasSubscribed;
            lock (MeterInstrumentCollection.Lock)
            {
                if (!instrument.IsObservable)
                {
                    wasSubscribed = UnsubscribeInstrument(instrument, out cookie);
                }
                else
                {
                    wasSubscribed = UnsubscribeObservableInstrument(instrument, out cookie);
                }
                if (wasSubscribed)
                {
                    _countSubscribedInstruments--;
                    if (_countSubscribedInstruments == 0)
                    {
                        MeterInstrumentCollection.Instance.RemoveInstrumentDisposedListener(this);
                    }
                }
            }

            if(wasSubscribed)
            {
                InstrumentMeasurementsComplete?.Invoke(instrument, cookie);
            }
        }



        public Action<MeterInstrument, MeterInstrumentListener>? MeterInstrumentPublished { get; set; }
        public Action<MeterInstrument, object?>? InstrumentMeasurementsComplete { get; set; }

        public void SetMeasurementEventCallback<T>(MeasurementCallback<T>? measurementFunc)
        {
            // measurementFunc might be null so we can't type test with 'is'
            if(typeof(T) == typeof(object))
            {
                if (measurementFunc is MeasurementCallback<object?> objFunc)
                {
                    _recordObjectFunc = objFunc;
                }
                else
                {
                    _recordObjectFunc = (i, m, l, c) => { };
                }
            }
            else if(typeof(T) == typeof(double))
            {
                if (measurementFunc is MeasurementCallback<double> doubleFunc)
                {
                    _recordDoubleFunc = doubleFunc;
                }
                else
                {
                    _recordDoubleFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
            else if (typeof(T) == typeof(float))
            {
                if (measurementFunc is MeasurementCallback<float> floatFunc)
                {
                    _recordFloatFunc = floatFunc;
                }
                else
                {
                    _recordFloatFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
            else if (typeof(T) == typeof(long))
            {
                if (measurementFunc is MeasurementCallback<long> longFunc)
                {
                    _recordLongFunc = longFunc;
                }
                else
                {
                    _recordLongFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
            else if (typeof(T) == typeof(int))
            {
                if (measurementFunc is MeasurementCallback<int> intFunc)
                {
                    _recordIntFunc = intFunc;
                }
                else
                {
                    _recordIntFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
            else if (typeof(T) == typeof(short))
            {
                if (measurementFunc is MeasurementCallback<short> shortFunc)
                {
                    _recordShortFunc = shortFunc;
                }
                else
                {
                    _recordShortFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
            else if (typeof(T) == typeof(byte))
            {
                if (measurementFunc is MeasurementCallback<byte> byteFunc)
                {
                    _recordByteFunc = byteFunc;
                }
                else
                {
                    _recordByteFunc = (instrument, measurement, labels, cookie) =>
                        _recordObjectFunc(instrument, measurement, labels, cookie);
                }
            }
        }

        public void RecordObservableInstruments()
        {
            // This ensures that meters can't be published/unpublished while we are trying to traverse the
            // list. The Observe callback could still be concurrent with Dispose().
            Dictionary<MeterInstrument, object?> subscriptionCopy;
            lock (_subscribedObservableMeters)
            {
                subscriptionCopy = new Dictionary<MeterInstrument, object?>(_subscribedObservableMeters);
            }
            foreach (KeyValuePair<MeterInstrument, object?> kv in subscriptionCopy)
            {
                MeterInstrument instrument = kv.Key;
                object? cookie = kv.Value;
                instrument.Observe(this, cookie);
            }
        }

        public void Dispose()
        {
            MeterInstrumentCollection.Instance.RemoveInstrumentedPublishedListener(this);
            MeterInstrumentCollection.Instance.VisitInstruments(i => OnMeterInstrumentDisposed(i));
        }

        private bool SubscribeObservableInstrument(MeterInstrument instrument, object? listenerCookie, out object? previousCookie)
        {
            lock (_subscribedObservableMeters)
            {
                bool ret = !_subscribedObservableMeters.TryGetValue(instrument, out previousCookie);
                _subscribedObservableMeters[instrument] = listenerCookie;
                return ret;
            }
        }

        private bool UnsubscribeObservableInstrument(MeterInstrument instrument, out object? cookie)
        {
            lock (_subscribedObservableMeters)
            {
                if (_subscribedObservableMeters.TryGetValue(instrument, out cookie))
                {
                    return _subscribedObservableMeters.Remove(instrument);
                }
                return false;
            }
        }

        private bool SubscribeInstrument(MeterInstrument instrument, object? cookie, out object? previousCookie)
        {
            return instrument.AddOrUpdateSubscription(this, cookie, out previousCookie);
        }

        private bool UnsubscribeInstrument(MeterInstrument instrument, out object? cookie)
        {
            return instrument.RemoveSubscription(this, out cookie);
        }

        internal void OnMeterInstrumentPublished(MeterInstrument instrument)
        {
            MeterInstrumentPublished?.Invoke(instrument, this);
        }

        internal void OnMeterInstrumentDisposed(MeterInstrument instrument)
        {
            DisableMeasurementEvents(instrument);
        }

        internal void OnMeasurement<T>(MeterInstrument instrument, T val, ReadOnlySpan<ValueTuple<string, object>> labels, object? cookie)
            where T : unmanaged
        {
            // All these type check conditionals can be resolved statically by the JIT once it knows the T type.
            // The body of the loop reduces to just one branch and all the rest are eliminated
            if (val is double dVal)
            {
                _recordDoubleFunc(instrument, dVal, labels, cookie);
            }
            else if (val is float fVal)
            {
                _recordFloatFunc(instrument, fVal, labels, cookie);
            }
            else if (val is long lVal)
            {
                _recordLongFunc(instrument, lVal, labels, cookie);
            }
            else if (val is int iVal)
            {
                _recordIntFunc(instrument, iVal, labels, cookie);
            }
            else if (val is short sVal)
            {
                _recordShortFunc(instrument, sVal, labels, cookie);
            }
            else if (val is byte bVal)
            {
                _recordByteFunc(instrument, bVal, labels, cookie);
            }
            else
            {
                _recordObjectFunc(instrument, val, labels, cookie);
            }
        }
    }
}
