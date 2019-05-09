// <copyright file="IResource.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Resources
{
    using System.Collections.Generic;
    using OpenCensus.Tags;

    /// <summary>
    /// Represents a resource, which captures identifying information about the entities
    /// for which signals(stats or traces) are reported. It further provides a framework for detection
    /// of resource information from the environment and progressive population as signals propagate from
    /// the core instrumentation library to a backend's exporter.
    /// </summary>
    public interface IResource
    {
        /// <summary>
        /// Gets Type identifier for the resource.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Gets the map of the labels/tags that describe the resource.
        /// </summary>
        IEnumerable<ITag> Tags { get; }
    }
}
