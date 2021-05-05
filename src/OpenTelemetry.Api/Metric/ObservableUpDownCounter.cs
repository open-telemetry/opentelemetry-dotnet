// <copyright file="ObservableUpDownCounter.cs" company="OpenTelemetry Authors">
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
#pragma warning disable SA1623, SA1611, SA1615

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// ObservableUpDownCounter is an observable Instrument that reports additive value(s)
    /// when the instrument is being observed.
    /// e.g. the process heap size.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public sealed class ObservableUpDownCounter<T> : ObservableInstrument<T>
        where T : unmanaged
    {
        private Func<IEnumerable<Measurement<T>>> observeValues;

        internal ObservableUpDownCounter(Meter meter, string name, Func<IEnumerable<Measurement<T>>> observeValues, string? description, string? unit)
            : base(meter, name, description, unit)
        {
            this.observeValues = observeValues;
            this.Publish();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        internal override IEnumerable<Measurement<T>> Observe()
        {
            return this.observeValues();
        }
    }
}
