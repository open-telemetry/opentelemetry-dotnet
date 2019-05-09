// <copyright file="ITagContextBuilder.cs" company="OpenCensus Authors">
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

    /// <summary>
    /// Tags context builder.
    /// </summary>
    public interface ITagContextBuilder
    {
        /// <summary>
        /// Puts a new tag into context.
        /// </summary>
        /// <param name="key">Key of the tag to add.</param>
        /// <param name="value">Value of the tag to add.</param>
        /// <returns>Tag context builder for operations chaining.</returns>
        ITagContextBuilder Put(ITagKey key, ITagValue value);

        /// <summary>
        /// Removes tag with the given key from the context.
        /// </summary>
        /// <param name="key">Key of the tag to remove.</param>
        /// <returns>Tag context builder for operations chaining.</returns>
        ITagContextBuilder Remove(ITagKey key);

        /// <summary>
        /// Builds the tags context.
        /// </summary>
        /// <returns>Resulting tag context.</returns>
        ITagContext Build();

        /// <summary>
        /// Builds tag context and save it as current.
        /// </summary>
        /// <returns>Scope control object. Dispose it to close a scope.</returns>
        IScope BuildScoped();
    }
}
