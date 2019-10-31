﻿// <copyright file="TagContextBase.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.DistributedContext
{
    public abstract class TagContextBase : ITagContext
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return "TagContext";
        }

        /// <inheritdoc/>
        public override bool Equals(object other)
        {
            if (!(other is TagContextBase))
            {
                return false;
            }

            var otherTags = (TagContextBase)other;

            var t1Enumerator = this.GetEnumerator();
            var t2Enumerator = otherTags.GetEnumerator();

            List<DistributedContextEntry> tags1 = null;
            List<DistributedContextEntry> tags2 = null;

            if (t1Enumerator == null)
            {
                tags1 = new List<DistributedContextEntry>();
            }
            else
            {
                tags1 = this.ToList();
            }

            if (t2Enumerator == null)
            {
                tags2 = new List<DistributedContextEntry>();
            }
            else
            {
                tags2 = otherTags.ToList();
            }

            if (tags1.Count != tags2.Count)
            {
                return false;
            }

            // TODO: this sounds scary, we should rework it along with Tags API
            var c1Dist = tags1.Distinct().ToArray();
            var c2Dist = tags2.Distinct().ToArray();
            return c1Dist.Count() == c2Dist.Count() && c1Dist.Intersect(c2Dist).Count() == c1Dist.Count();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var t in this)
            {
                hashCode += t.GetHashCode();
            }

            return hashCode;
        }

        public abstract IEnumerator<DistributedContextEntry> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
