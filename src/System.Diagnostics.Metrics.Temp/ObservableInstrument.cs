// <copyright file="ObservableInstrument.cs" company="OpenTelemetry Authors">
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

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// ObservableInstrument_T is the base class from which all observable instruments will inherit from.
    /// It will only support the CLS compliant numerical types.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public abstract class ObservableInstrument<T> : Instrument
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableInstrument{T}"/> class.
        /// Protected constructor to create the instrument with the common properties.
        /// </summary>
        protected ObservableInstrument(
            Meter meter,
            string name,
            string? unit,
            string? description)
            : base(meter, name, description, unit)
        {
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public override bool IsObservable => true;

        /// <summary>
        /// Observe() fetches the current measurements being tracked by this instrument.
        /// </summary>
        internal abstract IEnumerable<Measurement<T>> Observe();
    }
}
