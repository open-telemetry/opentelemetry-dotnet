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
    public readonly struct Link
    {
        private static readonly IDictionary<string, object> EmptyAttributes = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> struct.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        public Link(in SpanContext spanContext)
            : this(spanContext, EmptyAttributes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> struct.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        /// <param name="attributes">Link attributes.</param>
        public Link(in SpanContext spanContext, IDictionary<string, object> attributes)
        {
            this.Context = spanContext.IsValid ? spanContext : default;
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

        /// <summary>
        /// Compare two <see cref="Link"/> for equality.
        /// </summary>
        /// <param name="link1">First link to compare.</param>
        /// <param name="link2">Second link to compare.</param>
        public static bool operator ==(Link link1, Link link2) => link1.Equals(link2);

        /// <summary>
        /// Compare two <see cref="Link"/> for not equality.
        /// </summary>
        /// <param name="link1">First link to compare.</param>
        /// <param name="link2">Second link to compare.</param>
        public static bool operator !=(Link link1, Link link2) => !link1.Equals(link2);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is Link))
            {
                return false;
            }

            Link that = (Link)obj;
            return that.Context == this.Context &&
                that.Attributes == this.Attributes;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.Context.GetHashCode();
            result = (31 * result) + this.Attributes.GetHashCode();
            return result;
        }
    }
}
