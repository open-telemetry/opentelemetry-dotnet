// <copyright file="MeterListener.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// A delegate to represent the callbacks signatures used in the listener.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public delegate void MeasurementCallback<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct;

    /// <summary>
    /// The listener class can be used to listen to observable and non-observable instrument
    /// recorded measurements.
    /// </summary>
    public sealed class MeterListener : IDisposable
    {
        internal static ConcurrentDictionary<MeterListener, bool> GlobalListeners = new ConcurrentDictionary<MeterListener, bool>();

        internal ConcurrentDictionary<Instrument, object?> Instruments = new ConcurrentDictionary<Instrument, object?>();
        internal ConcurrentDictionary<Type, object?> Callbacks = new ConcurrentDictionary<Type, object?>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MeterListener"/> class.
        /// </summary>
        public MeterListener()
        {
            MeterListener.GlobalListeners.TryAdd(this, true);
        }

        /// <summary>
        /// Callbacks to get notification when an instrument is published.
        /// </summary>
        public Action<Instrument, MeterListener>? InstrumentPublished { get; set; }

        /// <summary>
        /// Callbacks to get notification when stopping the measurement on some instrument
        /// this can happen when the Meter or the Listener is disposed of. Or calling Stop()
        /// on the listener.
        /// </summary>
        // This need some clarification
        public Action<Instrument, object?>? MeasurementsCompleted { get; set; }

        /// <summary>
        /// Start listening to a specific instrument measurement recording.
        /// </summary>
        public void EnableMeasurementEvents(Instrument instrument, object? state = null)
        {
            this.Instruments.TryAdd(instrument, state);
            instrument.Register(this);
        }

        /// <summary>
        /// Stop listening to a specific instrument measurement recording.
        /// returns the associated state.
        /// </summary>
        public object? DisableMeasurementEvents(Instrument instrument)
        {
            this.Instruments.TryRemove(instrument, out var state);
            return state;
        }

        /// <summary>
        /// Set a callback for a specific numeric type to get the measurement recording notification
        /// from all instruments which enabled listened to and was created with the same specified
        /// numeric type. If a measurement of type T is recorded and a callback of type T is registered,
        /// that callback is used. If there is no callback for type T but there is a callback for type
        /// object, the measured value is boxed and reported via the object typed callback. If there is
        /// neither type T callback nor object callback then the measurement will not be reported.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public void SetMeasurementEventCallback<T>(MeasurementCallback<T>? measurementCallback)
            where T : struct
        {
            this.Callbacks.TryAdd(typeof(T), (object?)measurementCallback);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Start()
        {
        }

        /// <summary>
        /// Call all Observable instruments to get the recorded measurements reported to the
        /// callbacks enabled by SetMeasurementEventCallback_T.
        /// </summary>
        public void RecordObservableInstruments()
        {
            foreach (var instrument in this.Instruments)
            {
                if (instrument.Key.IsObservable)
                {
                    if (instrument.Key is ObservableInstrument<int> iInst)
                    {
                        if (this.Callbacks.TryGetValue(typeof(int), out var callback))
                        {
                            if (callback is MeasurementCallback<int> intCallback)
                            {
                                var measurements = iInst.Observe();

                                var state = instrument.Value;
                                foreach (var m in measurements)
                                {
                                    intCallback(instrument.Key, m.Value, m.Tags, instrument.Value);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Dispose()
        {
            MeterListener.GlobalListeners.TryRemove(this, out var _);
        }
    }
}
