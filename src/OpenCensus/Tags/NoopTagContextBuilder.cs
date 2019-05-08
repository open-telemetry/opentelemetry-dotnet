// <copyright file="NoopTagContextBuilder.cs" company="OpenCensus Authors">
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

    internal sealed class NoopTagContextBuilder : TagContextBuilderBase
    {
        internal static readonly ITagContextBuilder Instance = new NoopTagContextBuilder();

        private NoopTagContextBuilder()
        {
        }

        public override ITagContextBuilder Put(ITagKey key, ITagValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return this;
        }

        public override ITagContextBuilder Remove(ITagKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this;
        }

        public override ITagContext Build()
        {
            return NoopTagContext.Instance;
        }

        public override IScope BuildScoped()
        {
            return NoopScope.Instance;
        }
    }
}
