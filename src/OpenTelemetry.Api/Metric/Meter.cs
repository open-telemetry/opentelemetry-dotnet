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

#nullable enable

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// TBD
    /// </summary>
    public class Meter : IDisposable
    {

        /// <summary>
        /// The constructor allows creating the Meter class with the name and optionally the version.
        /// The name should be validated as described by the OpenTelemetry specs.
        /// </summary>
        public Meter(string name)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public Meter(string name, string? version)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Getter properties to retrieve the Meter name and version
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// TBD.
        /// </summary>
        public string? Version { get; }

        /// <summary>
        /// Factory Methods to create Counter and Histogram instruments.
        /// </summary>
        public Counter<T> CreateCounter<T>(
            string name,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public Histogram<T> CreateHistogram<T>(
            string name,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Factory Methods to create an observable Counter instrument.
        /// </summary>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableCounter<T> CreateObservableCounter<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Factory Methods to create observable gauge instrument.
        /// </summary>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableGauge<T> CreateObservableGauge<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Factory Methods to create observable UpDownCounter instrument.
        /// </summary>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<T> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<Measurement<T>> observeValue,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ObservableUpDownCounter<T> CreateObservableUpDownCounter<T>(
            string name,
            Func<IEnumerable<Measurement<T>>> observeValues,
            string? description = null,
            string? unit = null) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
