// <copyright file="Tags.cs" company="OpenCensus Authors">
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
    using OpenCensus.Tags.Propagation;

    public sealed class Tags
    {
        private static readonly object Lock = new object();

        private static Tags tags;

        private readonly ITagsComponent tagsComponent = new TagsComponent();

        internal Tags(bool enabled)
        {
            if (enabled)
            {
                this.tagsComponent = new TagsComponent();
            }
            else
            {
                this.tagsComponent = NoopTags.NewNoopTagsComponent();
            }
        }

        internal Tags()
            : this(false)
        {
        }

        public static ITagger Tagger
        {
            get
            {
                Initialize(true);
                return tags.tagsComponent.Tagger;
            }
        }

        public static ITagPropagationComponent TagPropagationComponent
        {
            get
            {
                Initialize(false);
                return tags.tagsComponent.TagPropagationComponent;
            }
        }

        public static TaggingState State
        {
            get
            {
                Initialize(false);
                return tags.tagsComponent.State;
            }
        }

        internal static void Initialize(bool enabled)
        {
            if (tags == null)
            {
                lock (Lock)
                {
                    tags = tags ?? new Tags(enabled);
                }
            }
        }
    }
}
