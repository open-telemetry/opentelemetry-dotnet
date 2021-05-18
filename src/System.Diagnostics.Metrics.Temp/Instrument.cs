// <copyright file="Instrument.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// Is the base class which contains all common properties between different types of instruments.
    /// It contains the protected constructor and the Publish method allows activating the instrument
    /// to start recording measurements.
    /// </summary>
    public abstract class Instrument
    {
        internal ConcurrentDictionary<MeterListener, bool> Listeners = new ConcurrentDictionary<MeterListener, bool>();
        internal KeyValuePair<MeterListener, bool>[] CachedListeners = new KeyValuePair<MeterListener, bool>[0];

        /// <summary>
        /// Initializes a new instance of the <see cref="Instrument"/> class.
        /// Protected constructor to initialize the common instrument properties.
        /// </summary>
        protected Instrument(Meter meter, string name, string? unit, string? description)
        {
            this.Meter = meter;
            this.Name = name;
            this.Unit = unit;
            this.Description = description;
        }

        /// <summary>
        /// Getters to retrieve the properties that the instrument is created with.
        /// </summary>
        public Meter Meter { get; }

        /// <summary>
        /// TBD.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// TBD.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// TBD.
        /// </summary>
        public string? Unit { get; }

        /// <summary>
        /// A property tells if a listener is listening to this instrument measurement recording.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// A property tells if the instrument is a regular instrument or an observable instrument.
        /// </summary>
        public virtual bool IsObservable => false;

        public void Register(MeterListener listener)
        {
            this.Listeners.TryAdd(listener, true);

            // use CachedListener to avoid an alloc when enumerating this.Listener in HotPath
            this.CachedListeners = this.Listeners.ToArray();
        }

        /// <summary>
        /// Publish is to allow activating the instrument to start recording measurements and to allow
        /// listeners to start listening to such measurements.
        /// </summary>
        protected void Publish()
        {
            foreach (var kv in MeterListener.GlobalListeners)
            {
                kv.Key.InstrumentPublished?.Invoke(this, kv.Key);
            }
        }
    }
}
