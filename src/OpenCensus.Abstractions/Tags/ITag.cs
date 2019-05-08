// <copyright file="ITag.cs" company="OpenCensus Authors">
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
    /// <summary>
    /// Tag with the key and value.
    /// </summary>
    public interface ITag
    {
        /// <summary>
        /// Gets the tag key.
        /// </summary>
        ITagKey Key { get; }

        /// <summary>
        /// Gets the tag value.
        /// </summary>
        ITagValue Value { get; }
    }
}
