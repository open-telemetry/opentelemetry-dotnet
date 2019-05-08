// <copyright file="TagContext.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Tags
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public sealed class TagContext : TagContextBase
    {
        public static readonly ITagContext Empty = new TagContext(new Dictionary<ITagKey, ITagValue>());

        public TagContext(IDictionary<ITagKey, ITagValue> tags)
        {
            this.Tags = new ReadOnlyDictionary<ITagKey, ITagValue>(new Dictionary<ITagKey, ITagValue>(tags));
        }

        public IDictionary<ITagKey, ITagValue> Tags { get; }

        public override IEnumerator<ITag> GetEnumerator()
        {
            var result = this.Tags.Select((kvp) => Tag.Create(kvp.Key, kvp.Value));
            return result.ToList().GetEnumerator();
        }
    }
}
