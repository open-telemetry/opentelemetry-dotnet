// <copyright file="Link.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Link associated with the span.
    /// </summary>
    public sealed class Link
    {
        private static readonly IDictionary<string, object> EmptyAttributes = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> class.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        public Link(SpanContext spanContext)
            : this(spanContext, EmptyAttributes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> class.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        /// <param name="attributes">Link attributes.</param>
        public Link(SpanContext spanContext, IDictionary<string, object> attributes)
        {
            this.Context = spanContext ?? SpanContext.BlankLocal;
            this.Attributes = attributes ?? EmptyAttributes;
        }

        /// <summary>
        /// Gets the span context of a linked span.
        /// </summary>
        public SpanContext Context { get; }

        /// <summary>
        /// Gets the collection of attributes associated with the link.
        /// </summary>
        public IDictionary<string, object> Attributes { get; }
    }
}
