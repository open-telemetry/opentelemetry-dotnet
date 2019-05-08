// <copyright file="ITagsComponent.cs" company="OpenCensus Authors">
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

    /// <summary>
    /// Tagger configuration.
    /// </summary>
    public interface ITagsComponent
    {
        /// <summary>
        /// Gets the tagger to operate with tags.
        /// </summary>
        ITagger Tagger { get; }

        /// <summary>
        /// Gets the propagation component to use to serialize and deserialize tags on the wire.
        /// </summary>
        ITagPropagationComponent TagPropagationComponent { get; }

        /// <summary>
        /// Gets the state of tagging API - enabled or disabled.
        /// </summary>
        TaggingState State { get; }
    }
}