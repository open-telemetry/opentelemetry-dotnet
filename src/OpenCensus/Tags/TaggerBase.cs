// <copyright file="TaggerBase.cs" company="OpenCensus Authors">
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

    public abstract class TaggerBase : ITagger
    {
        public abstract ITagContext Empty { get; }

        public abstract ITagContext CurrentTagContext { get; }

        public abstract ITagContextBuilder EmptyBuilder { get; }

        public abstract ITagContextBuilder CurrentBuilder { get; }

        public abstract ITagContextBuilder ToBuilder(ITagContext tags);

        public abstract IScope WithTagContext(ITagContext tags);
    }
}
