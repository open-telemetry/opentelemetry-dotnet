// <copyright file="LinkNew.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Link associated with the span.
    /// </summary>
    public readonly struct LinkNew
    {
        internal readonly ActivityLink ActivityLink;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkNew"/> struct.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        public LinkNew(in SpanContextNew spanContext)
        {
            this.ActivityLink = new ActivityLink(spanContext.ActivityContext);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkNew"/> struct.
        /// </summary>
        /// <param name="spanContext">Span context of a linked span.</param>
        /// <param name="attributes">Link attributes.</param>
        public LinkNew(in SpanContextNew spanContext, IDictionary<string, object> attributes)
        {
            this.ActivityLink = new ActivityLink(spanContext.ActivityContext, attributes);
        }

        /// <summary>
        /// Gets the span context of a linked span.
        /// </summary>
        public SpanContextNew Context
        {
            get
            {
                return new SpanContextNew(this.ActivityLink.Context);
            }
        }

        /// <summary>
        /// Gets the collection of attributes associated with the link.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes
        {
            get
            {
                return this.ActivityLink.Attributes;
            }
        }

        /// <summary>
        /// Compare two <see cref="LinkNew"/> for equality.
        /// </summary>
        /// <param name="link1">First link to compare.</param>
        /// <param name="link2">Second link to compare.</param>
        public static bool operator ==(LinkNew link1, LinkNew link2) => link1.Equals(link2);

        /// <summary>
        /// Compare two <see cref="LinkNew"/> for not equality.
        /// </summary>
        /// <param name="link1">First link to compare.</param>
        /// <param name="link2">Second link to compare.</param>
        public static bool operator !=(LinkNew link1, LinkNew link2) => !link1.Equals(link2);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return this.ActivityLink.Equals(obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.ActivityLink.GetHashCode();
        }
    }
}
