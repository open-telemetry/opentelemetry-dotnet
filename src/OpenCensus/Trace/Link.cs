// <copyright file="Link.cs" company="OpenCensus Authors">
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
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using OpenCensus.Utils;

    public sealed class Link : ILink
    {
        private static readonly IDictionary<string, IAttributeValue> EmptyAttributes = new Dictionary<string, IAttributeValue>();

        private Link(ITraceId traceId, ISpanId spanId, LinkType type, IDictionary<string, IAttributeValue> attributes)
        {
            this.TraceId = traceId ?? throw new ArgumentNullException(nameof(traceId));
            this.SpanId = spanId ?? throw new ArgumentNullException(nameof(spanId));
            this.Type = type;
            this.Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        public ITraceId TraceId { get; }

        public ISpanId SpanId { get; }

        public LinkType Type { get; }

        public IDictionary<string, IAttributeValue> Attributes { get; }

        public static ILink FromSpanContext(ISpanContext context, LinkType type)
        {
            return new Link(context.TraceId, context.SpanId, type, EmptyAttributes);
        }

        public static ILink FromSpanContext(ISpanContext context, LinkType type, IDictionary<string, IAttributeValue> attributes)
        {
            IDictionary<string, IAttributeValue> copy = new Dictionary<string, IAttributeValue>(attributes);
            return new Link(
                context.TraceId,
                context.SpanId,
                type,
                new ReadOnlyDictionary<string, IAttributeValue>(copy));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Link{"
                + "traceId=" + this.TraceId + ", "
                + "spanId=" + this.SpanId + ", "
                + "type=" + this.Type + ", "
                + "attributes=" + Collections.ToString(this.Attributes)
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Link that)
            {
                return this.TraceId.Equals(that.TraceId)
                     && this.SpanId.Equals(that.SpanId)
                     && this.Type.Equals(that.Type)
                     && this.Attributes.SequenceEqual(that.Attributes);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.TraceId.GetHashCode();
            h *= 1000003;
            h ^= this.SpanId.GetHashCode();
            h *= 1000003;
            h ^= this.Type.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            return h;
        }
    }
}
