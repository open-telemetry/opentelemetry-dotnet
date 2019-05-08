// <copyright file="NoopTagger.cs" company="OpenCensus Authors">
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
    using OpenCensus.Common;
    using OpenCensus.Internal;

    internal sealed class NoopTagger : TaggerBase
    {
        internal static readonly ITagger Instance = new NoopTagger();

        public override ITagContext Empty
        {
            get
            {
                return NoopTags.NoopTagContext;
            }
        }

        public override ITagContext CurrentTagContext
        {
            get
            {
                return NoopTags.NoopTagContext;
            }
        }

        public override ITagContextBuilder EmptyBuilder
        {
            get
            {
                return NoopTags.NoopTagContextBuilder;
            }
        }

        public override ITagContextBuilder CurrentBuilder
        {
            get
            {
                return NoopTags.NoopTagContextBuilder;
            }
        }

        public override ITagContextBuilder ToBuilder(ITagContext tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            return NoopTags.NoopTagContextBuilder;
        }

        public override IScope WithTagContext(ITagContext tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            return NoopScope.Instance;
        }
    }
}
