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

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// The counter is a non-observable Instrument that supports non-negative increments.
    /// e.g. Number of completed requests.
    /// </summary>
    public sealed class Counter<T> : Instrument<T> where T : unmanaged
    {
        internal Counter(Meter meter, string name, string? description, string? unit)
            : base(meter, name, description, unit)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(T measurement)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            KeyValuePair<string, object?> tag1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            KeyValuePair<string, object?> tag1,
            KeyValuePair<string, object?> tag2)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public void Add(
            T measurement,
            params KeyValuePair<string, object?>[] tags)
        {
            throw new NotImplementedException();
        }
    }
}
