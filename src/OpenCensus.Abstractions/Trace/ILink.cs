// <copyright file="ILink.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System.Collections.Generic;

    /// <summary>
    /// Link associated with the span.
    /// </summary>
    public interface ILink
    {
        /// <summary>
        /// Gets the trace ID of the linked span.
        /// </summary>
        ITraceId TraceId { get; }

        /// <summary>
        /// Gets the span ID of the linked span.
        /// </summary>
        ISpanId SpanId { get; }

        /// <summary>
        /// Gets the type of the link.
        /// </summary>
        LinkType Type { get; }

        /// <summary>
        /// Gets the collection of attributes associated with the link.
        /// </summary>
        IDictionary<string, IAttributeValue> Attributes { get; }
    }
}
