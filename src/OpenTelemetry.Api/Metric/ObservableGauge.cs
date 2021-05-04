// <copyright file="ObservableGauge.cs" company="OpenTelemetry Authors">
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
    /// ObservableGauge is an observable Instrument that reports non-additive value(s)
    /// when the instrument is being observed.
    /// e.g. the current room temperature
    /// </summary>
    public sealed class ObservableGauge<T> : ObservableInstrument<T> where T : unmanaged
    {
        internal ObservableGauge(Meter meter, string name, string? description, string? unit)
            : base(meter, name, description, unit)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        protected override IEnumerable<Measurement<T>> Observe()
        {
            throw new NotImplementedException();
        }
    }
}
