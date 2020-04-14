// <copyright file="CorrelationContext.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Correlation context.
    /// </summary>
    public readonly struct CorrelationContext : IEquatable<CorrelationContext>
    {
        private static readonly List<CorrelationContextEntry> EmptyList = new List<CorrelationContextEntry>();
        private readonly List<CorrelationContextEntry> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationContext"/> struct.
        /// </summary>
        /// <param name="entries">Entries for correlation context.</param>
        internal CorrelationContext(List<CorrelationContextEntry> entries)
        {
            this.entries = entries;
        }

        /// <summary>
        /// Gets empty object of <see cref="CorrelationContext"/> struct.
        /// </summary>
        public static CorrelationContext Empty { get; } = new CorrelationContext(EmptyList);

        /// <summary>
        /// Gets all the <see cref="CorrelationContextEntry"/> in this <see cref="CorrelationContext"/>.
        /// </summary>
        public IEnumerable<CorrelationContextEntry> Entries => this.entries;

        /// <summary>
        /// Gets the <see cref="CorrelationContextEntry"/> with the specified name.
        /// </summary>
        /// <param name="key">Name of the <see cref="CorrelationContextEntry"/> to get.</param>
        /// <returns>The <see cref="string"/> with the specified name. If not found - null.</returns>
        public string GetEntryValue(string key) => this.entries.LastOrDefault(x => x.Key == key).Value;

        /// <inheritdoc/>
        public bool Equals(CorrelationContext other)
        {
            if (this.entries.Count() != other.entries.Count())
            {
                return false;
            }

            foreach (CorrelationContextEntry entry in this.entries)
            {
                if (other.GetEntryValue(entry.Key) != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
