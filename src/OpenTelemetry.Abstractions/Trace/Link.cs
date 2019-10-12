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
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Link associated with the span.
    /// </summary>
    public sealed class Link
    {
        private static readonly IDictionary<string, object> EmptyAttributes = new Dictionary<string, object>();

        public Link(SpanContext spanContext)
            : this(spanContext, EmptyAttributes)
        {
        }

        public Link(SpanContext spanContext, IDictionary<string, object> attributes)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (!spanContext.IsValid)
            {
                throw new ArgumentException(nameof(spanContext));
            }

            this.Context = spanContext;
            this.Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        /// <summary>
        /// Gets the span context of a linked span.
        /// </summary>
        public SpanContext Context { get; }

        /// <summary>
        /// Gets the collection of attributes associated with the link.
        /// </summary>
        public IDictionary<string, object> Attributes { get; }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Link that)
            {
                return this.Context.Equals(that.Context)
                     && this.Attributes.SequenceEqual(that.Attributes);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Context.TraceId.GetHashCode();
            h *= 1000003;
            h ^= this.Context.SpanId.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            return h;
        }
    }
}
