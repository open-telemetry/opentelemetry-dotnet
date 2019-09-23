// <copyright file="Decision.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Sampling decision.
    /// </summary>
    public struct Decision
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Decision"/> struct.
        /// </summary>
        /// <param name="isSampled">True if sampled, false otherwise.</param>
        public Decision(bool isSampled)
        {
            this.IsSampled = isSampled;
            this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Decision"/> struct.
        /// </summary>
        /// <param name="isSampled">True if sampled, false otherwise.</param>
        /// <param name="attributes">Attributes associated with the sampling decision.</param>
        public Decision(bool isSampled, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            this.IsSampled = isSampled;

            // TODO: it's a great thing to copy the list. But may not worth it as it
            // makes another memory allocation.
            this.Attributes = new List<KeyValuePair<string, object>>(attributes);
        }

        /// <summary>
        /// Gets a value indicating whether Span was sampled or not.
        /// The value is not suppose to change over time and can be cached.
        /// </summary>
        public bool IsSampled { get; private set; }

        /// <summary>
        /// Gets a map of attributes associated with the sampling decision.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; private set; }
    }
}
