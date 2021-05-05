// <copyright file="InstrumentT.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

#nullable enable
#pragma warning disable SA1649

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// Instrument_T is the base class from which all non-observable instruments will inherit from.
    /// Mainly It'll support the CLS compliant numerical types.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public abstract class Instrument<T> : Instrument
        where T : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Instrument{T}"/> class.
        /// Protected constructor to create the instrument with the common properties.
        /// </summary>
        protected Instrument(Meter meter, string name, string? description, string? unit)
            : base(meter, name, description, unit)
        {
        }

        /// <summary>
        /// Record measurement overloads allowing passing different numbers of tags.
        /// </summary>
        protected void RecordMeasurement(T measurement)
        {
            var ros = new KeyValuePair<string, object?>[0];
            this.RecordMeasurement(measurement, new ReadOnlySpan<KeyValuePair<string, object?>>(ros));
        }

        /// <summary>
        /// TBD.
        /// </summary>
        protected void RecordMeasurement(
            T measurement,
            KeyValuePair<string, object?> tag1)
        {
            var ros = new KeyValuePair<string, object?>[1];
            ros[0] = tag1;
            this.RecordMeasurement(measurement, new ReadOnlySpan<KeyValuePair<string, object?>>(ros));
        }

        /// <summary>
        /// TBD.
        /// </summary>
        protected void RecordMeasurement(
            T measurement,
            KeyValuePair<string, object?> tag1,
            KeyValuePair<string, object?> tag2)
        {
            var ros = new KeyValuePair<string, object?>[2];
            ros[0] = tag1;
            ros[1] = tag2;
            this.RecordMeasurement(measurement, new ReadOnlySpan<KeyValuePair<string, object?>>(ros));
        }

        /// <summary>
        /// TBD.
        /// </summary>
        protected void RecordMeasurement(
            T measurement,
            KeyValuePair<string, object?> tag1,
            KeyValuePair<string, object?> tag2,
            KeyValuePair<string, object?> tag3)
        {
            var ros = new KeyValuePair<string, object?>[3];
            ros[0] = tag1;
            ros[1] = tag2;
            ros[2] = tag3;
            this.RecordMeasurement(measurement, new ReadOnlySpan<KeyValuePair<string, object?>>(ros));
        }

        /// <summary>
        /// TBD.
        /// </summary>
        protected void RecordMeasurement(
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var listener in this.Listeners)
            {
                if (listener.Key.Instruments.TryGetValue(this, out var state))
                {
                    if (listener.Key.Callbacks.TryGetValue(typeof(T), out var callback))
                    {
                        if (callback is MeasurementCallback<T> callbackT)
                        {
                            callbackT(this, measurement, tags, state);
                        }
                    }
                }
            }
        }
    }
}
