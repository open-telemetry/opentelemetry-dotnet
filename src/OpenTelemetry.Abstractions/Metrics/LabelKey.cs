// <copyright file="LabelKey.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// The key of a label associated with a <see cref="IMetric{T}"/>.
    /// </summary>
    public sealed class LabelKey
    {
        private LabelKey()
        {
        }

        /// <summary>
        /// Gets a key of the label.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Gets a human-readable description of what this label key represents.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Creates a new instance of a <see cref="LabelKey"/>.
        /// </summary>
        /// <param name="key">The key of the label.</param>
        /// <param name="description">A human-readable description of what this label key represents.</param>
        /// <returns>A new instance of <see cref="LabelKey"/>.</returns>
        public static LabelKey Create(string key, string description)
        {
            return new LabelKey()
            {
                Key = key,
                Description = description,
            };
        }
    }
}
