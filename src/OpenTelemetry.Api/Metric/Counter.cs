// <copyright file="Counter.cs" company="OpenTelemetry Authors">
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
    /// The counter is a non-observable Instrument that supports non-negative increments.
    /// e.g. Number of completed requests.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public sealed class Counter<T> : Instrument<T>
        where T : unmanaged
    {
        internal Counter(Meter meter, string name, string? description, string? unit)
            : base(meter, name, description, unit)
        {
            this.Publish();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(T measurement)
        {
            this.RecordMeasurement(measurement);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            KeyValuePair<string, object?> tag1)
        {
            this.RecordMeasurement(measurement, tag1);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            KeyValuePair<string, object?> tag1,
            KeyValuePair<string, object?> tag2)
        {
            this.RecordMeasurement(measurement, tag1, tag2);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            KeyValuePair<string, object?> tag1,
            KeyValuePair<string, object?> tag2,
            KeyValuePair<string, object?> tag3)
        {
            this.RecordMeasurement(measurement, tag1, tag2, tag3);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            this.RecordMeasurement(measurement, tags);
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            params KeyValuePair<string, object?>[] tags)
        {
            this.RecordMeasurement(measurement, tags);
        }
    }
}
