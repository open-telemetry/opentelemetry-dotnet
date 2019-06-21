// <copyright file="TagContextBase.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tags
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Utils;

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

            List<Tag> tags1 = null;
            List<Tag> tags2 = null;

            if (t1Enumerator == null)
            {
                tags1 = new List<Tag>();
            }
            else
            {
                tags1 = this.ToList();
            }

            if (t2Enumerator == null)
            {
                tags2 = new List<Tag>();
            }
            else
            {
                tags2 = otherTags.ToList();
            }

            return Collections.AreEquivalent(tags1, tags2);
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

        public abstract IEnumerator<Tag> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
