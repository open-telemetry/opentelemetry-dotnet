// <copyright file="SpanCreationOptions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Span creation options for advanced scenarios.
    /// </summary>
    public class SpanCreationOptions
    {
        /// <summary>
        /// Gets or sets explicit span start timestamp.
        /// Use it when span has started in the past and created later.
        /// </summary>
        public DateTimeOffset StartTimestamp { get; set; }

        /// <summary>
        /// Gets or sets list of <see cref="Link"/>.
        /// </summary>
        public IEnumerable<Link> Links { get; set; }

        /// <summary>
        /// Gets or sets attributes known prior to span creation.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; set; }

        /// <summary>
        /// Gets or sets Links factory. Use it to deserialize list of <see cref="Link"/> lazily
        /// when application configures OpenTelemetry implementation that supports links.
        /// </summary>
        public Func<IEnumerable<Link>> LinksFactory { get; set; }
    }
}
