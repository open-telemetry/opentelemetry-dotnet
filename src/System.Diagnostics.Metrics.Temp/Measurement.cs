// <copyright file="Measurement.cs" company="OpenTelemetry Authors">
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

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// A measurement stores one observed value and its associated tags. This type is used by Observable instruments' Observe() method when reporting current measurements.
    /// with the associated tags.
    /// </summary>
    /// <typeparam name="T">TBD.</typeparam>
    public struct Measurement<T>
        where T : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Measurement{T}"/> struct.
        /// Construct the Measurement using the value and the list of tags.
        /// We'll always copy the input list as this is not perf hot path.
        /// </summary>
        public Measurement(T value, IEnumerable<KeyValuePair<string, object?>> tags)
        {
            this.Value = value;
            this.TagArray = Enumerable.ToArray(tags);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Measurement{T}"/> struct.
        /// </summary>
        public Measurement(T value, params KeyValuePair<string, object?>[] tags)
        {
            this.Value = value;
            this.TagArray = tags;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Measurement{T}"/> struct.
        /// </summary>
        public Measurement(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            this.Value = value;
            this.TagArray = tags.ToArray();
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public ReadOnlySpan<KeyValuePair<string, object?>> Tags
        {
            get
            {
                return new ReadOnlySpan<KeyValuePair<string, object?>>(this.TagArray);
            }
        }

        /// <summary>
        /// TBD.
        /// </summary>
        public T Value { get; }

        private KeyValuePair<string, object?>[] TagArray { get; }
    }
}
