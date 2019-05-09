// <copyright file="Tagger.cs" company="OpenCensus Authors">
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
    using OpenCensus.Common;
    using OpenCensus.Internal;

    public sealed class Tagger : TaggerBase
    {
        private readonly CurrentTaggingState state;

        internal Tagger(CurrentTaggingState state)
        {
            this.state = state;
        }

        public override ITagContext Empty
        {
            get { return TagContext.Empty; }
        }

        public override ITagContext CurrentTagContext
        {
            get
            {
                return this.state.Internal == TaggingState.DISABLED
                    ? TagContext.Empty
                    : ToTagContext(CurrentTagContextUtils.CurrentTagContext);
            }
        }

        public override ITagContextBuilder EmptyBuilder
        {
            get
            {
                return this.state.Internal == TaggingState.DISABLED
                    ? NoopTagContextBuilder.Instance
                    : new TagContextBuilder();
            }
        }

        public override ITagContextBuilder CurrentBuilder
        {
            get
            {
                return this.state.Internal == TaggingState.DISABLED
                    ? NoopTagContextBuilder.Instance
                    : this.ToBuilder(CurrentTagContextUtils.CurrentTagContext);
            }
        }

        public override ITagContextBuilder ToBuilder(ITagContext tags)
        {
            return this.state.Internal == TaggingState.DISABLED
                ? NoopTagContextBuilder.Instance
                : ToTagContextBuilder(tags);
        }

        public override IScope WithTagContext(ITagContext tags)
        {
            return this.state.Internal == TaggingState.DISABLED
                ? NoopScope.Instance
                : CurrentTagContextUtils.WithTagContext(ToTagContext(tags));
        }

        private static ITagContext ToTagContext(ITagContext tags)
        {
            if (tags is TagContext)
            {
                return tags;
            }
else
            {
                TagContextBuilder builder = new TagContextBuilder();
                foreach (var tag in tags)
                {
                    if (tag != null)
                    {
                        builder.Put(tag.Key, tag.Value);
                    }
                }

                return builder.Build();
            }
        }

        private static ITagContextBuilder ToTagContextBuilder(ITagContext tags)
        {
            // Copy the tags more efficiently in the expected case, when the TagContext is a TagContextImpl.
            if (tags is TagContext)
            {
                return new TagContextBuilder(((TagContext)tags).Tags);
            }
else
            {
                TagContextBuilder builder = new TagContextBuilder();
                foreach (var tag in tags)
                {
                    if (tag != null)
                    {
                        builder.Put(tag.Key, tag.Value);
                    }
                }

                return builder;
            }
        }
    }
}
