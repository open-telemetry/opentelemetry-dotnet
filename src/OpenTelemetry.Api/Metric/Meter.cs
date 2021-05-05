// <copyright file="Meter.cs" company="OpenTelemetry Authors">
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
using System.Linq;

#nullable enable
#pragma warning disable SA1623, SA1611, SA1615

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// TBD.
    /// </summary>
    public class Meter : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Meter"/> class.
        /// The constructor allows creating the Meter class with the name and optionally the version.
        /// The name should be validated as described by the OpenTelemetry specs.
        /// </summary>
        public Meter(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Meter"/> class.
        /// </summary>
        public Meter(string name, string? version)
        {
            this.Name = name;
            this.Version = version;
        }

        /// <summary>
        /// Gets properties to retrieve the Meter name and version.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets TBD.
        /// </summary>
        public string? Version { get; }

        /// <summary>
        /// Factory Methods to create Counter and Histogram instruments.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public Counter<T> CreateCounter<T>(
            string name,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new Counter<T>(this, name, description, unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public Histogram<T> CreateHistogram<T>(
            string name,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new Histogram<T>(this, name, description, unit);
        }

        /// <summary>
        /// Factory Methods to create an observable Counter instrument.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableCounter<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        new Measurement<T>(observeValue()),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableCounter<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        observeValue(),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableCounter<T>(this, name, observeValues, description, unit);
        }

        /// <summary>
        /// Factory Methods to create observable gauge instrument.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableGauge<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        new Measurement<T>(observeValue()),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableGauge<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        observeValue(),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableGauge<T>(this, name, observeValues, description, unit);
        }

        /// <summary>
        /// Factory Methods to create observable UpDownCounter instrument.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableUpDownCounter<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        new Measurement<T>(observeValue()),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableUpDownCounter<T>(
                this,
                name,
                () => new List<Measurement<T>>()
                    {
                        observeValue(),
                    },
                description,
                unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        /// <typeparam name="T">TBD.</typeparam>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null)
            where T : unmanaged
        {
            return new ObservableUpDownCounter<T>(this, name, observeValues, description, unit);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Dispose()
        {
            foreach (var listenerKV in MeterListener.GlobalListeners)
            {
                var pending = listenerKV.Key.Instruments.Where((k) => k.Key.Meter == this);
                foreach (var instKV in pending)
                {
                    listenerKV.Key.Instruments.TryRemove(instKV.Key, out _);
                    listenerKV.Key.MeasurementsCompleted?.Invoke(instKV.Key, instKV.Value);
                }
            }
        }
    }
}
