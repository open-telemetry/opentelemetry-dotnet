// <copyright file="TagContextBuilder.cs" company="OpenCensus Authors">
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
    using System;
    using System.Collections.Generic;
    using OpenCensus.Common;

    internal sealed class TagContextBuilder : TagContextBuilderBase
    {
        internal TagContextBuilder(IDictionary<ITagKey, ITagValue> tags)
        {
            this.Tags = new Dictionary<ITagKey, ITagValue>(tags);
        }

        internal TagContextBuilder()
        {
            this.Tags = new Dictionary<ITagKey, ITagValue>();
        }

        internal IDictionary<ITagKey, ITagValue> Tags { get; }

        public override ITagContextBuilder Put(ITagKey key, ITagValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.Tags[key] = value ?? throw new ArgumentNullException(nameof(value));
            return this;
        }

        public override ITagContextBuilder Remove(ITagKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (this.Tags.ContainsKey(key))
            {
                this.Tags.Remove(key);
            }

            return this;
        }

        public override ITagContext Build()
        {
            return new TagContext(this.Tags);
        }

        public override IScope BuildScoped()
        {
            return CurrentTagContextUtils.WithTagContext(this.Build());
        }
    }
}
