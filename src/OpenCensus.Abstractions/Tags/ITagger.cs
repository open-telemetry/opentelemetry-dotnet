// <copyright file="ITagger.cs" company="OpenCensus Authors">
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
    /// Tags API configuraiton.
    /// </summary>
    public interface ITagger
    {
        /// <summary>
        /// Gets the empty tags context.
        /// </summary>
        ITagContext Empty { get; }

        /// <summary>
        /// Gets the current tags context.
        /// </summary>
        ITagContext CurrentTagContext { get; }

        /// <summary>
        /// Gets the empty tags builder.
        /// </summary>
        ITagContextBuilder EmptyBuilder { get; }

        /// <summary>
        /// Gets the builder out of current context.
        /// </summary>
        ITagContextBuilder CurrentBuilder { get; }

        /// <summary>
        /// Enters the scope of code where the given tag context is in the current context and
        /// returns an object that represents that scope.The scope is exited when the returned object is
        /// closed.
        /// </summary>
        /// <param name="tags">Tags to set as current.</param>
        /// <returns>Scope object. Dispose to dissassociate tags context from the current context.</returns>
        IScope WithTagContext(ITagContext tags);

        /// <summary>
        /// Gets the builder from the tags context.
        /// </summary>
        /// <param name="tags">Tags to pre-initialize builder with.</param>
        /// <returns>Tags context builder preinitialized with the given tags.</returns>
        ITagContextBuilder ToBuilder(ITagContext tags);
    }
}
