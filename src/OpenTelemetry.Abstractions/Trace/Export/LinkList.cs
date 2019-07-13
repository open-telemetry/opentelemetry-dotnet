// <copyright file="LinkList.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public sealed class LinkList : ILinks
    {
        private static readonly LinkList Empty = new LinkList(new ILink[0], 0);

        internal LinkList(IEnumerable<ILink> links, int droppedLinksCount)
        {
            this.Links = links ?? throw new ArgumentNullException("Null links");
            this.DroppedLinksCount = droppedLinksCount;
        }

        public int DroppedLinksCount { get; }

        public IEnumerable<ILink> Links { get; }

        public static LinkList Create(IEnumerable<ILink> links, int droppedLinksCount)
        {
            if (links == null)
            {
                return Empty;
            }

            IEnumerable<ILink> copy = new List<ILink>(links);

            return new LinkList(copy, droppedLinksCount);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Links{"
                + "links=" + this.Links + ", "
                + "droppedLinksCount=" + this.DroppedLinksCount
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is LinkList that)
            {
                return this.Links.SequenceEqual(that.Links)
                     && (this.DroppedLinksCount == that.DroppedLinksCount);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Links.GetHashCode();
            h *= 1000003;
            h ^= this.DroppedLinksCount;
            return h;
        }
    }
}
