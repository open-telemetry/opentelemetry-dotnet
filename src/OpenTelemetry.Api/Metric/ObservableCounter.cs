// <copyright file="ObservableCounter.cs" company="OpenTelemetry Authors">
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
    /// ObservableCounter is an observable Instrument that reports monotonically increasing value(s)
    /// when the instrument is being observed.
    /// e.g. CPU time (for different processes, threads, user mode or kernel mode).
    /// </summary>
    public sealed class ObservableCounter<T> : ObservableInstrument<T> where T : unmanaged
    {
        internal ObservableCounter(Meter meter, string name, string? description, string? unit)
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
