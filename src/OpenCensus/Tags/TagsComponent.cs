// <copyright file="TagsComponent.cs" company="OpenCensus Authors">
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

    public class TagsComponent : TagsComponentBase
    {
        // The TaggingState shared between the TagsComponent, Tagger, and TagPropagationComponent
        private readonly CurrentTaggingState state;
        private readonly ITagger tagger;
        private readonly ITagPropagationComponent tagPropagationComponent;

        public TagsComponent()
        {
            this.state = new CurrentTaggingState();
            this.tagger = new Tagger(this.state);
            this.tagPropagationComponent = new TagPropagationComponent(this.state);
        }

        public override ITagger Tagger
        {
            get { return this.tagger; }
        }

        public override ITagPropagationComponent TagPropagationComponent
        {
            get { return this.tagPropagationComponent; }
        }

        public override TaggingState State
        {
            get { return this.state.Value; }
        }
    }
}
